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
