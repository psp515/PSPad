# Offline-first session — design

Slice 1 shipped a client that cannot be opened without a network. This spec
replaces the boot and session model that caused it. It covers what gates the
app on open, where identity lives when the server is unreachable, how the
access token is renewed without an iframe, and what the user sees at each
stage of a cold start.

It does not change the command pipeline, storage shapes, sync protocol or the
HTTP surface — `specs/slice-design.md` remains authoritative for those.

---

## 1. Problem

Four symptoms, reported together:

1. The app is unusable without a network connection. It is meant to behave
   like Microsoft To Do: open, show your data, sync later.
2. On open, the application bar and drawer render with no content, then the
   app redirects to the login page.
3. Login is required on every cold start of the installed PWA.
4. After signing in, the screen reads `Completing login...` in unstyled
   browser text.

## 2. Root cause

One conflation, expressed as two independent network-dependent gates.

The client treats *having a live OIDC access token* as equivalent to *being a
known user on this device*. These are different questions. The first is only
answerable online and only matters when calling the API. The second is
answerable offline and is the one that should decide whether the app opens.

**Gate one — the token.** `App.razor`'s `AuthorizeRouteView` renders
`NotAuthorized` → `RedirectToLogin.razor`, which navigates to
`authentication/login`, which redirects the browser to Keycloak. The
authenticated signal comes from the OIDC library, which reads `sessionStorage`
(`Microsoft.AspNetCore.Components.WebAssembly.Authentication` defaults its
`userStore` to `WebStorageStateStore({store: sessionStorage})`; there is no
supported .NET API to change it). A home-screen-installed PWA is a fresh
browsing context on each launch — `display: "standalone"` in
`manifest.webmanifest` — so `sessionStorage` is empty, the gate fails, and the
browser is sent to a login page it may not be able to reach. This is symptom 3,
and symptom 1 whenever the device is offline.

The library's fallback, `automaticSilentRenew`, renews through a hidden iframe
against Keycloak's authorize endpoint with `prompt=none`. It needs the Keycloak
session cookie to be readable in a cross-origin iframe, which mobile browsers
routinely block in standalone mode. It fails silently and offers nothing
offline regardless.

**Gate two — `/api/me`.** `AppShell.razor:118` gates `_ready` and
`Coordinator.Start()` on a successful account fetch. `FetchAccountAsync`
(`:146`) retries three times, and on failure sets `_accountUnavailable` and
returns **without ever setting `_ready`**. Offline, the shell has already
rendered its bar and drawer by then, so the user is left looking at empty
chrome. This is symptom 2, and it is independent of gate one: fixing the token
alone still leaves the app stuck here.

Symptom 4 is `RemoteAuthenticatorView`'s default text fragments, never
overridden. [ADR-0024](../adr/0024-authentication-callback-route-off-appshell.md)
recorded this as acceptable ("a brief, unstyled 'processing' message") on the
reasoning that it is brief. On a cold mobile start behind a WASM download it is
not brief.

## 3. Model

Two concepts, separated.

| | `LocalSession` | Access token |
|---|---|---|
| Answers | Who is this device's user? | May I call the API right now? |
| Storage | IndexedDB | `sessionStorage`, library-managed |
| Lifetime | Until logout or the trust window lapses | Minutes |
| Readable offline | Yes | Irrelevant offline |
| Gates | Opening the app | API calls only |

Absence of an access token means *offline*. It never means *log in again*.

`LocalSession` holds the user id, display name, email, time zone, the OIDC
refresh token, and `LastServerContactUtc`.

The user id half of this already exists: `IReplica.OwnerAsync()` persists the
owning user id in IndexedDB per
[ADR-0018](../adr/0018-purge-local-replica-on-user-switch.md), consumed by
`ReplicaOwnership.EnsureCurrentUserAsync`. `LocalSession` is that seam widened
to carry the rest of the identity, not a new parallel store.

## 4. Decisions

**D1 — `LocalSession` gates the app, not the token.** A custom
`AuthenticationStateProvider` builds its principal from `LocalSession`. Only
the `AuthenticationStateProvider` registration is replaced;
`IRemoteAuthenticationService` stays the library's, so `RemoteAuthenticatorView`
and the interactive login redirect are untouched.

**D2 — The boot decision precedes the first render.** `index.html`'s existing
`.pspad-boot` splash is not torn down when WASM starts. It is torn down by the
client once the decision in §5 is made. Application chrome therefore never
renders before the app knows whether it is opening or redirecting, which
removes symptom 2 by construction rather than by adding a loading flag.

**D3 — The offline trust window is 7 days.** If `LastServerContactUtc` is more
than 7 days old, local identity has expired and a real sign-in is required.
Rationale: the app opens offline indefinitely in normal use, because any
successful refresh or sync resets the clock; the cap bounds how long a lost
device keeps serving readable data.

**D4 — Expiry clears the replica, never the outbox.** On expiry the replica is
cleared: it is server-derived and disposable by AD-6, and it is the data worth
denying to a stolen device. The outbox is locally authored intent that exists
nowhere else, so it survives. On the next sign-in `ReplicaOwnership` decides
its fate under the existing rule — same user, it flushes; different user, it is
purged per ADR-0018 and
[ADR-0025](../adr/0025-purge-on-any-user-mismatch-including-no-recorded-owner.md).
This spec adds no new purge path.

**D5 — A failed refresh is not a sign-out unless Keycloak says so.** A `400`
carrying `invalid_grant` is authoritative revocation: clear `LocalSession` and
require sign-in. Any transport failure, timeout, `5xx`, or offline condition is
not: keep the session, surface offline state, retry later. Conflating these
signs the user out every time they open the app in a tunnel.

**D6 — Renewal is a direct token-endpoint call.** `POST` to
`{Authority}/protocol/openid-connect/token` with `grant_type=refresh_token` and
`client_id`, on an `HttpClient` with no authorization handler attached. No
iframe, no cookie dependency, identical behaviour in standalone and tab
contexts. Keycloak rotates the refresh token on use, so the response's new
refresh token is written back to `LocalSession` in the same step.

The resulting access token is held in memory by `TokenRefresher` and attached
by our own `DelegatingHandler`, replacing the library's
`AuthorizationMessageHandler` on `PSPadApiClient`. Nothing is written back
into the library's `sessionStorage` entry.

**D7 — The library's renewal never starts outside the auth routes.**
`OidcProviderOptions` exposes no `AutomaticSilentRenew` switch — the setting
is hard-coded in the shipped `AuthenticationService.js` — so the iframe
renewal cannot be turned off by configuration. It is instead never armed: the
JS `UserManager` that owns the renewal timer is constructed when the library's
`IRemoteAuthenticationService` / `IAccessTokenProvider` is first resolved.
D1 removes the state-provider resolution and D6's own handler removes the
`AuthorizationMessageHandler` resolution, leaving `RemoteAuthenticatorView` on
`/authentication/*` as the only thing that constructs it. Two renewal
mechanisms therefore never run against one rotating refresh token.

This is the reason token attachment is ours rather than the library's: keeping
`AuthorizationMessageHandler` would resolve `IAccessTokenProvider` on the first
API call and arm the very timer this decision exists to avoid.

**D8 — `/api/me` refreshes identity, it does not gate.** `AppShell` reads
identity from `LocalSession` and is ready immediately. `/api/me` is called
opportunistically when online and updates both `AppState` and `LocalSession`.
Its failure sets offline state and nothing else. `_accountUnavailable` as a
terminal boot state is removed.

**D9 — Auth screens carry the brand mark.** `Authentication.razor` supplies
explicit `LoggingIn`, `CompletingLoggingIn`, `LogOut` and `LogOutSucceeded`
fragments rendering the same mark as `.pspad-boot`. This closes symptom 4 and
completes `specs/ui-polish-design.md`'s intent of one brand mark from first
paint through loading. It amends ADR-0024's consequence that the unstyled
message is acceptable; the route arrangement that ADR decided is unchanged.

**D10 — Realm session lifetimes are set explicitly.**
`docker/keycloak/realm-psplace.json` gains `ssoSessionIdleTimeout` of 30 days
and `ssoSessionMaxLifespan` of 90 days rather than inheriting Keycloak's
short defaults. The realm's idle timeout must exceed D3's window, or the
server invalidates the refresh token first and D3 stops being the effective
bound. The realm is shared across applications per
[ADR-0026](../adr/0026-one-shared-realm-for-every-self-hosted-app.md), so this
applies to every client in `psplace` — intended, as a self-hosted personal
tool wants long sessions generally.

## 5. Boot sequence

```
index.html paints .pspad-boot (brand mark)
  │
  ▼
WASM boots — splash held, no chrome rendered
  │
  ▼
read LocalSession from IndexedDB
  │
  ├── none ─────────────────────────────────► login (splash → login, no chrome)
  │
  ├── stale (now − LastServerContactUtc > 7d)
  │     └── clear replica + LocalSession, keep outbox ──────► login
  │
  └── fresh
        │
        ├── tear down splash, render app, Today from replica
        │
        └── background, non-blocking:
              refresh token → token endpoint
                ├── success ──► store rotated token, set LastServerContactUtc,
                │               cache access token, /api/me, start sync
                ├── transport failure ──► offline indicator, retry on reconnect
                └── 400 invalid_grant ──► clear LocalSession ──► login
```

Nothing on the left-hand path touches the network. The right-hand branch is
entirely background: no step of it can block, delay or reverse the decision to
open the app.

## 6. Components

All new types live in `src/PSPad.App/Auth/`, following the feature-folder
grouping established by `State/`.

| Type | Responsibility |
|---|---|
| `LocalSession` | The record: user id, display name, email, time zone, refresh token, `LastServerContactUtc` |
| `LocalSessionStore` | IndexedDB read/write/clear via JS interop, in the manner of `IndexedDbReplica` |
| `SessionBootstrapper` | Runs §5's decision before first render; owns the splash handoff |
| `LocalAuthenticationStateProvider` | `AuthenticationStateProvider` built from `LocalSession` |
| `TokenRefresher` | D6's token-endpoint exchange, D5's failure classification, in-memory access-token cache |
| `SessionAuthorizationHandler` | `DelegatingHandler` attaching the cached token to `PSPadApiClient`, replacing `AuthorizationMessageHandler` |

`SessionBootstrapper` uses the registered `IClock`, never `DateTime.UtcNow`,
and the registered `IConnectivity` rather than its own connectivity probe.

The capture path: the refresh token is read out of the library's
`sessionStorage` entry (`oidc.user:{authority}:{client_id}`) once, immediately
after a successful interactive login, and written into `LocalSession`. No
supported API exposes it — `AccessToken` carries only `Value`, `Expires` and
`GrantedScopes` — so this is the single point where the library's internal
storage shape is relied upon. It is isolated in one interop function, and its
failure mode is visible immediately: the next cold start falls back to login.

## 7. Error handling

| Condition | Behaviour |
|---|---|
| No `LocalSession` | Login. Not an error; first run. |
| `LocalSession` stale past 7 days | Clear replica and session, keep outbox, login. |
| Refresh: transport failure, timeout, `5xx` | Keep session, offline indicator, retry on reconnect. |
| Refresh: `400 invalid_grant` | Clear session, login. |
| `/api/me` fails | Offline state only. Never blocks readiness. |
| IndexedDB unreadable | Treat as no session. Login. |

## 8. Testing

Unit and bUnit only. Nothing here touches MongoDB or the API host, so there is
no Testcontainers surface.

- `SessionBootstrapper`'s decision table: absent, fresh, and stale session each
  produce the right branch, against a fake `IClock`.
- The 7-day boundary, including exactly-at-the-boundary.
- D5's classification: `invalid_grant` clears, transport failure does not.
- D4: expiry clears the replica and leaves the outbox intact.
- `LocalAuthenticationStateProvider` reports authenticated from a session alone,
  with no token present.
- `AppShell` reaches ready with `/api/me` failing.

Manual verification, since none of the above exercises a real standalone PWA:
install on a phone, sign in, force-quit, enable airplane mode, reopen. The app
must land on Today with data and an offline indicator — never on a login screen.

## 9. Out of scope

- Mid-session renewal timing beyond D6's on-demand refresh. Access tokens are
  short-lived; refreshing on API call and on reconnect is sufficient and the
  iframe mechanism is gone either way.
- Any change to the sync protocol, conflict rules or command pipeline.
- Offline sign-in for a device that has never signed in. Impossible: the first
  token exchange requires Keycloak.

## 10. Documentation obligations

D10 changes what a self-hoster runs, so `docs/src/pages/install.astro` needs
the session-lifetime note in the same piece of work. D3's behaviour is a user-
visible capability, so `features.astro` gains the offline-session statement.
Per AGENTS.md §11 these ship with the change, not after it.
