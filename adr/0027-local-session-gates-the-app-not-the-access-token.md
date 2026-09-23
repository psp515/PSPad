---
title: A durable local session, not the access token, decides whether the app opens
tags: [identity, offline, sync, ui]
date: 2026-09-22
status: Active
---

# ADR-0027: A durable local session, not the access token, decides whether the app opens

## Context

The client is specified offline-first: an installed PWA that opens on a phone
with no network, serves the Today screen from its IndexedDB replica, and syncs
when connectivity returns. It does not do this. Opened without a network — or
simply opened a second time from the home screen — it lands on a login page,
and offline that page cannot even load.

The cause is that *holding a live OIDC access token* is used as a proxy for
*being a known user on this device*. Those are different questions with
different answers offline, and the client asks the first one twice on the way
to its first render.

`App.razor`'s `AuthorizeRouteView` asks it through the OIDC library, whose
`userStore` defaults to `sessionStorage` and cannot be redirected by any
supported .NET API. `manifest.webmanifest` declares `display: "standalone"`,
and a home-screen launch is a fresh browsing context, so that store is empty on
every cold start. The failure sends the browser to Keycloak. The library's
compensating mechanism, `automaticSilentRenew`, runs through a hidden iframe
with `prompt=none` and depends on a cross-origin session cookie that mobile
browsers routinely block in standalone mode; it also offers nothing at all when
there is no network.

`AppShell.razor:118` asks it again, gating `_ready` and `Coordinator.Start()`
on a successful `/api/me`. Offline the fetch fails three times and returns
without setting `_ready`, leaving the already-rendered bar and drawer wrapped
around nothing. This gate is independent: repairing token persistence alone
still deadlocks here.

The user id needed to answer the second question is *already* persisted
offline. `IReplica.OwnerAsync()` records the owning user in IndexedDB under
[ADR-0018](0018-purge-local-replica-on-user-switch.md), for
`ReplicaOwnership.EnsureCurrentUserAsync` to compare against. Nothing consults
it at boot.

## Decision

We will make a durable `LocalSession` in IndexedDB — user id, display name,
email, time zone, refresh token, and the timestamp of last successful server
contact — the sole gate on whether the application opens, and reduce the access
token to what it is: a credential for calling the API, whose absence means
*offline* and never *sign in again*.

A custom `AuthenticationStateProvider` builds its principal from that session.
Only that registration is replaced; `IRemoteAuthenticationService` remains the
library's, so the interactive sign-in redirect and callback are untouched.
Renewal becomes a direct `grant_type=refresh_token` call to Keycloak's token
endpoint, and a refresh failure is treated as a sign-out only when Keycloak
answers `400 invalid_grant` — never on a transport failure.

That gate is enforced by `@attribute [Authorize]` on every routable page except
`/authentication/{action}` and the not-found page, so a device with no local
session at all is redirected to sign in rather than rendering the shell around
empty data. Before this decision the de-facto gate was `AppShell`'s `/api/me`
fetch, which had to be removed for the app to open offline; without an explicit
attribute in its place, `AuthorizeRouteView`'s `NotAuthorized` branch never runs
and `RedirectToLogin` is unreachable. A guard test asserts the attribute is
present on every routable page, because a new page silently added without it is
the way this hole reopens.

The iframe renewal has no supported off-switch — `OidcProviderOptions` exposes
no `AutomaticSilentRenew`, the setting being hard-coded in the shipped
`AuthenticationService.js` — so it is disarmed rather than disabled. Its timer
belongs to a JS `UserManager` built when the library's
`IRemoteAuthenticationService` / `IAccessTokenProvider` is first resolved;
replacing the state provider and attaching bearer tokens with our own
`DelegatingHandler` instead of `AuthorizationMessageHandler` leaves
`RemoteAuthenticatorView` on `/authentication/*` as the only resolver, so the
timer never arms during normal use.

Both of those library services are registered by `AddRemoteAuthentication` as
factories that cast `AuthenticationStateProvider` to them, so overriding that
registration poisons both: `IRemoteAuthenticationService` broke interactive
login outright, and `IAccessTokenProvider` is its dormant twin. Both are
therefore registered explicitly, resolving the library's own service out of
`GetServices<AuthenticationStateProvider>()` rather than by cast.

`specs/offline-first-session-design.md`'s D7 is correct for cold starts only.
The shipped `AuthenticationService.js` constructs its `UserManager` with no
`automaticSilentRenew` override, so oidc-client-ts's default `true` applies
and that library prefers the `refresh_token` grant when one is present. After
an interactive login `RemoteAuthenticatorView` has constructed that
`UserManager`, so its renewal timer *is* armed for the remainder of that
page's life, against the very refresh token we just captured. This is harmless
only because `docker/keycloak/realm-psplace.json` leaves `revokeRefreshToken`
at Keycloak's default `false`: with revocation on, the library renewing from
the pre-rotation token would invalidate the chain our `LocalSession` holds.
Turning that setting on is therefore not a realm-only change.

Signing out explicitly clears the `LocalSession` and the replica and keeps the
outbox, exactly as the trust window lapsing does, and it happens before the
browser leaves for Keycloak's end-session endpoint so an interrupted or
offline sign-out is still a sign-out.

Local identity expires after **7 days** without successful server contact. On
expiry the replica is cleared and the outbox is kept, leaving its disposition
to the existing ownership rules in ADR-0018 and
[ADR-0025](0025-purge-on-any-user-mismatch-including-no-recorded-owner.md).

The boot splash in `index.html` is held until this decision resolves, so
application chrome never renders ahead of it, and `Authentication.razor`
supplies branded fragments in place of the library's default text.

`specs/offline-first-session-design.md` carries the full sequence, component
list and test obligations.

## Considered alternatives

- **Point the library's `userStore` at `localStorage`** by shipping a patched
  `AuthenticationService.js`. The smallest change that makes a second launch
  find its tokens. Rejected: it fixes only the online cold start. Offline the
  stored access token is expired, renewal is the same blocked iframe, and the
  app still redirects to an unreachable login page. It treats symptom 3 and
  leaves symptom 1, which is the one that makes the app not a daily driver.
- **Persist the refresh token and exchange it at boot, keeping the token as the
  gate.** Considered first and taken far enough to design. Rejected on the
  observation that killed it: with no network the exchange fails, so the gate
  fails, so the user is sent to the login page exactly as before. It makes
  online cold starts seamless and changes nothing about the offline case, which
  is the requirement.
- **Hand-roll the whole OIDC flow**, dropping `AddOidcAuthentication` and
  `RemoteAuthenticatorView` for our own authorize redirect and PKCE exchange.
  Rejected: replacing the state provider achieves the same control over the
  gate, and rewriting vetted security-sensitive redirect and code-exchange
  handling buys nothing this problem needs.
- **Trust local identity indefinitely, until explicit sign-out.** The closest
  match to how comparable apps behave and the best experience available.
  Rejected for the exposure it leaves: a lost or stolen phone serves readable
  GTD content forever, and the app has no remote wipe to compensate.

## Consequences

The app opens offline with data, which is the point. The chrome-before-decision
flash disappears by construction rather than by another readiness flag, the
iframe renewal mechanism leaves the codebase entirely, and one brand mark
covers first paint through sign-in. `AppShell` stops being able to deadlock on
`/api/me`, so `_accountUnavailable` as a terminal boot state is removed.

The cost is a refresh token at rest in IndexedDB. It is readable by any script
on the origin, exactly as the `sessionStorage` tokens are today, but it lives
far longer, so an XSS foothold is worth more than it used to be. The 7-day
window is the bound on that, and it is the whole mitigation — worth revisiting
if the client ever renders untrusted content.

The client also depends, at one point, on the shape of the library's own
`sessionStorage` entry, because no supported API exposes the refresh token
after login. A library upgrade can break that capture. It is isolated to a
single interop function and fails loudly on the next cold start, but it is a
genuine maintenance hostage and the reason to re-check it on every bump of
`Microsoft.AspNetCore.Components.WebAssembly.Authentication`.

A user who stays offline past 7 days loses the replica and must reach the
network to sign in again. No authored work is lost — the outbox survives and
flushes once they do — but the app is unusable in the meantime, which is the
deliberate price of not choosing indefinite trust. Setting the realm's idle
timeout to 30 days keeps the server from invalidating the refresh token before
that window is reached; because `psplace` is shared under
[ADR-0026](0026-one-shared-realm-for-every-self-hosted-app.md), that lifetime
applies to every application in the realm.
