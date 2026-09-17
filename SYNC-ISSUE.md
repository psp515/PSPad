<<<<<<< HEAD
# Sync issue: "different user" after first login — resolved

Root cause and fix: [adr/0022-authentication-callback-route-off-appshell.md](adr/0022-authentication-callback-route-off-appshell.md).

Summary: `AppShell` resolves the signed-in account exactly once, in
`OnInitializedAsync`, and is the `DefaultLayout` for every route including
`/authentication/{action}` (it can't be `[Authorize]`-guarded — that would be
circular). On a first-ever sign-in, Keycloak's redirect lands the browser on
that exact route with a fresh WASM boot. `RemoteAuthenticatorView` — a child
of `AppShell`'s `@Body` — is what actually exchanges the code and stores the
session, and until it finishes the cascading `AuthenticationState` reads
unauthenticated. `AppShell`'s one-shot check almost always wins that race,
locking `AppState.UserId` at `Guid.Empty` for the rest of the session: the
client-side navigation `RemoteAuthenticatorView` does afterwards never
remounts the layout to re-check. Every command from then on embeds the wrong
user id and the server rejects it forever as "That command is for a different
user" — a page refresh only "fixes" it because it lands on a normal route
where the OIDC session is already established.

Fix: `Authentication.razor` now uses its own minimal layout
(`Layout/AuthenticationLayout.razor`) instead of `AppShell`, so `AppShell`
never mounts on the login-callback route and the race no longer has anywhere
to happen. Regression test:
`test/PSPad.App.Tests/Pages/AuthenticationTests.cs`.

The investigation notes that led here (leading hypothesis, ruled-out
candidates, on-device capture checklist) are preserved in this file's git
history for anyone retracing the reasoning.
=======
# Sync issue: "different user" after first login

## Symptom (reported)

After a first login on a device:
- User data (areas etc.) doesn't appear to load.
- Commands created shortly after login are rejected by the server with
  "That command is for a different user."
- Refreshing the page fixes the *view* (everything gets fetched correctly),
  but commands created **before** the refresh are never acknowledged — the
  "failed synchronization" snackbar keeps firing.

This is on top of `03a1426` (don't start `SyncCoordinator` before account
ownership is known), which is already merged into this branch and did not
fix it.

## What's already ruled out

- **Deterministic user id.** `User.IdFor(subject)` (MD5 of the JWT subject)
  is used both by `ClaimsCurrentUser.UserId` (server, used to authorize
  every command) and by `UserProvisioner`/`MeEndpoints` to build the
  `MeResponse.UserId` the client stores in `AppState.UserId`. Read through
  both paths — they compute the same value from the same claim, so this
  isn't a hash/derivation bug.
- **Outbox-blocks-forever.** `SyncService.PushAsync` already drops a command
  outright when the rejection is `Unrecoverable: true` (which "different
  user" is) — see `src/PSPad.App/Sync/SyncService.cs:49-57`. So a *single*
  bad command doesn't explain a message that fires on every ~60s poll
  forever. For the message to keep recurring, **new** bad commands must
  keep entering the outbox, not the same one being retried.

## Leading hypothesis

`AppShell.LoadAccountAsync` (`src/PSPad.App/Layout/AppShell.razor:117-143`)
does, in order:

1. `State.UserId = me.UserId` (correct value from `/api/me`)
2. `Ownership.EnsureCurrentUserAsync(me.UserId)`
3. `ReloadAreasAndCountsAsync()` — loads `_areas` via
   `AreaStore.LoadAllAsync(State.UserId, ...)`, which reads the **local**
   IndexedDB replica only
4. `_ready = true` (UI renders `@Body`)
5. `Coordinator.Start()` — only now does the first pull/push actually talk
   to the server

On a genuinely first-ever login the local replica is empty at step 3 — no
pull has happened yet — so `_areas` is `[]` and the screen looks like data
"didn't load", even though nothing is wrong. That's cosmetic and should
self-heal once `Coordinator.Start()`'s first `RunAsync()` pulls and fires
`Coordinator.Changed` → `ReloadAreasAndCountsAsync()` again. **This alone
doesn't explain the "different user" rejections** — worth confirming/ruling
out on-device though, since the empty-screen-looks-broken part matches
"user data is not fetched".

For the actual rejections, the working theory is that `State.UserId`
somehow diverges from the server's `ICurrentUser.UserId` for the rest of
the session after a first login, so every command the user creates keeps
using the wrong id and keeps getting rejected — until a page refresh
re-runs `LoadAccountAsync` from scratch and gets it right. Candidates for
*why* it diverges, roughly in order of suspicion:

1. **`AppState` isn't actually a singleton in practice.** It's registered
   `AddScoped<AppState>()` in `Program.cs`. Blazor WASM has one root scope
   for the whole app, so this *should* behave like a singleton — but worth
   confirming nothing (a background task, a second component tree, hot
   reload) resolves a second scope and therefore a second `AppState`
   instance with `UserId == Guid.Empty`.
2. **A stale command queued before `State.UserId` was set.** Check whether
   any component/button that creates a command is reachable before
   `_ready` is true — `@Body` is gated on `_ready`, and `NavSidebar`/
   `TaskDetailPanel` take `Disabled="@(!_ready)"`, but worth verifying in
   the real browser that nothing (e.g. a keyboard shortcut, a queued click
   from before render, PWA background sync) can call `Sender.SendAsync`
   while `State.UserId` is still `Guid.Empty`.
3. **`ReplicaOwnership` meta round-trip through real IndexedDB.** The
   bUnit tests for `ReplicaOwnership`/`AppShell` all use `InMemoryReplica`,
   never the real `IndexedDbReplica` + `wwwroot/js/replica.js`. Worth
   checking with real devtools whether `getMeta('owner')` on a brand-new
   database really resolves to `null` on the C# side (not some non-null
   default that could make `EnsureCurrentUserAsync` take the "different
   user" branch and clear things unexpectedly), and whether
   `SetOwnerAsync`/`SetMarkerAsync` actually commit before the first
   `Coordinator.Start()` push fires.
4. **Token/claim change mid-session.** Confirm the JWT `sub` claim
   (`ClaimsCurrentUser.Subject`) is identical between the `/api/me` call
   and the later `/commands` POST for the same login. If the access token
   gets silently refreshed between the two calls and the refreshed token
   carries a different `sub` (shouldn't happen with Keycloak, but this is
   exactly the kind of thing that only shows up with a real IdP), every
   post-refresh command would be rejected even though the client never
   changed `State.UserId`.

## What to capture on-device to settle it

1. Open a **brand-new** account (or clear IndexedDB + Keycloak session) and
   watch Network + Console from the very first navigation.
2. Record the `/api/me` response body (`userId` field).
3. Immediately create something (new area / quick capture).
4. When the "different user" snackbar fires, capture:
   - The `/commands` POST request body (the `userId` inside the command
     payload) for the rejected entry.
   - Whether that value matches step 2's `userId`, or is `00000000-...`,
     or is some other value entirely.
5. In Application → IndexedDB → `pspad` → `meta`, note the `owner` row's
   value at the moment of the failure, and whether it matches step 2.
6. Note whether `_areas` was empty in the UI at the moment of failure (to
   confirm/deny the cosmetic-empty-screen part of the hypothesis
   separately from the rejection part).

Whichever of `State.UserId`, the `meta.owner` row, and the JWT `sub` turns
out to be the odd one out tells us which of the four candidates above is
real.
>>>>>>> 2c907e7 (docs: capture sync/different-user debugging notes)
