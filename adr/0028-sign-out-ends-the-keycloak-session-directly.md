---
title: Sign-out ends the Keycloak session directly, and anonymous visitors land on a public welcome screen
tags: [identity, offline, ui, security]
date: 2026-09-23
status: Active (amends ADR-0027's sign-out consequence)
---

# ADR-0028: Sign-out ends the Keycloak session directly, and anonymous visitors land on a public welcome screen

## Context

ADR-0027 made a durable `LocalSession` in IndexedDB — not the access token —
decide whether the app opens. It left sign-out described as "clear the local
session and hand the rest to `RemoteAuthenticatorView`". That turned out not to
work, for two independent reasons.

**The library only signs out a user it can still see.**
`RemoteAuthenticatorViewCore.ProcessLogOut` reads the cascading authentication
state and contacts the identity provider only when it still says authenticated:

```csharp
var state = await AuthenticationProvider.GetAuthenticationStateAsync();
if (state.User.Identity?.IsAuthenticated ?? false) { /* SignOutAsync */ }
else { Navigation.NavigateTo(returnUrl); }
```

Clearing locally first — which ADR-0027 wanted, so that an interrupted or
offline sign-out is still a sign-out — makes that branch fall through. Keycloak
is never told anything. Its SSO cookie survives, the next `[Authorize]` bounce
reaches `authentication/login`, Keycloak silently re-authenticates against its
own cookie, and the app signs the same user back in and redownloads their data.
Sign-out looked like a no-op with a delay.

**The library cannot sign out an app that was relaunched.** The OIDC user the
library needs lives in `sessionStorage`, which dies with the tab. That is
precisely the case ADR-0027 exists to serve: force-quit the PWA, reopen it, and
the `LocalSession` carries the app while the library's user store is empty. In
that state `signoutRedirect` has no `id_token_hint` to send, and the sign-out
path depends on a metadata fetch the offline-first app may not be able to make.
The library's sign-out works only in the one session that performed the
interactive login — the exception, not the rule.

Separately, every unauthenticated route bounced straight to the identity
provider. Combined with the live SSO cookie this was invisible; once sign-out
genuinely ends the Keycloak session, the bounce lands a signed-out visitor on a
Keycloak login form with no way back to anything that explains what they are
looking at.

## Decision

**Sign-out navigates to Keycloak's end-session endpoint itself.**
`KeycloakEndSession.UrlFor` builds
`{authority}/protocol/openid-connect/logout?client_id=…&post_logout_redirect_uri=…`,
and `SettingsPage` clears local storage and then leaves for it with
`forceLoad: true`. `client_id` is what lets Keycloak validate the return address
without an `id_token_hint`, so the relaunched app can sign out exactly as well
as the one that signed in. The URL shape is Keycloak's, the same assumption
`TokenRefresher` already makes about the token endpoint.

**Clearing still happens first, and offline sign-out stays local.** The storage
clear precedes the redirect, so a round trip that never comes back still leaves
the device signed out. When `IConnectivity` reports no network the remote leg is
skipped entirely: the app navigates to the welcome screen and announces the
anonymous state. That leaves Keycloak's session alive until the next online
sign-out — accepted, because the alternative is a browser error page and a user
who cannot tell whether anything happened.

**Clearing the local session also clears the library's credentials.**
`session.js`'s `clear()` removes every `oidc.`-prefixed `sessionStorage` entry
alongside the IndexedDB row. A refresh token the app no longer acknowledges is
still a credential sitting on the device.

**`/welcome` is the public face of a signed-out app.** It is routable,
carries no `[Authorize]`, and renders under `PublicLayout` rather than
`AppShell` — the shell loads a session, starts sync and draws the sidebar, none
of which exist for this visitor. `RedirectToLogin` now sends `NotAuthorized`
there with the intended destination in `returnUrl`, and the screen's Log in
action carries that through to `authentication/login`. Self-hosting lives on the
documentation site, which the screen links to; whoever reaches this page is
already standing in an instance.

## Considered alternatives

- **Keep `RemoteAuthenticatorView` and announce the anonymous state later** —
  cheapest change, and it fixes the silent re-login for the tab that performed
  the interactive login. It does nothing for the relaunched app, which is the
  case ADR-0027 was written for, and it leaves sign-out depending on a
  `sessionStorage` entry the app has already declared non-authoritative.
- **Announce the anonymous state after handing over, keep clearing last** —
  lets the library see an authenticated user and reach Keycloak. It also means a
  sign-out interrupted by a closed tab or a dead network leaves the local session
  intact: the device stays signed in after the user asked it not to be. Local
  clearing must not be conditional on a remote round trip.
- **Send `NotAuthorized` straight to `authentication/login`, as before** — one
  click fewer for a returning user whose seven-day trust window lapsed. It also
  means a signed-out visitor never sees the application at all, and gives the
  sign-out flow nowhere to land.

## Consequences

Sign-out now ends both sessions, so the next login asks for credentials. The
local replica and session are gone before the app leaves, and the OIDC
credentials with them.

`ApplicationPaths.LogOutCallbackPath` is no longer part of the normal flow.
`Authentication.razor` keeps `OnLogOutSucceeded` wired to `LocalSignOut.ClearAsync`
and `Keycloak:PostLogoutRedirectUri` keeps pointing at it, so the library's own
path stays correct if anything reaches it; nothing does today.

`post_logout_redirect_uri` must be an allowed redirect for the `pspad-frontend`
client. Keycloak falls back to the client's `redirectUris` when
`post.logout.redirect.uris` is unset, and the realm's `${APP_BASE_ADDRESS}/*`
covers `/welcome`, so no realm change is needed.

An offline sign-out leaves the Keycloak session alive. Coming back online and
signing in again will not prompt for credentials until that session expires or
an online sign-out ends it.

A guard test keeps `/welcome` and `authentication/{action}` the only routable
pages without `[Authorize]`, and a layout test keeps `/welcome` off `AppShell`
for the same reason ADR-0024 keeps the login callback off it.
