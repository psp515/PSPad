---
title: Sign-out revokes the refresh token, names the session it ends, wipes the outbox, and needs a connection
tags: [identity, offline, security, ui]
date: 2026-10-13
status: Active (amends ADR-0028)
---

# ADR-0071: Sign-out revokes the refresh token, names the session it ends, wipes the outbox, and needs a connection

## Context

ADR-0028 made sign-out clear local storage and then navigate to Keycloak's
end-session endpoint, with offline sign-out staying local. Issue #135 (OWASP
A07) found three gaps on a shared device:

- **The outbox survived.** The signed-out user's unsent commands stayed in
  IndexedDB, readable in DevTools, until a different user signed in.
- **The refresh token was never revoked**, online or offline. An offline
  sign-out left both the token and the SSO session alive with no later
  cleanup.
- **End-session carried no `id_token_hint`.** Keycloak then shows its own
  "Do you want to log out?" page, and the SSO session survives a user who
  closes that tab without confirming. ADR-0028 chose `client_id` alone
  because the relaunched app had no OIDC user, and so no id token, in
  `sessionStorage`.

Checked against Keycloak 26.0 with the shipped realm: the refresh response
carries an `id_token`; `/revoke` accepts a public client's `client_id`,
answers CORS for the app's origin, and returns 200 even for an unknown token;
an end-session request with `id_token_hint` redirects straight back without
the confirmation page; and an unset `post.logout.redirect.uris` falls back to
the client's `redirectUris`, so `/welcome` stays allowed.

## Decision

**The id token is part of the `LocalSession`.** `TokenRefresher` returns the
`id_token` of every exchange; the login capture and `SessionAuthorizationHandler`
store it next to the refresh token (a refresh carrying none keeps the stored
one). End-session gets `id_token_hint` whenever the session has one, plus
`client_id` and `post_logout_redirect_uri` as before. Keycloak accepts an
expired id token as a hint, so a stale one still works.

**Sign-out revokes the refresh token first.** `LocalSignOut.SignOutAsync`
POSTs the stored refresh token to `{authority}/protocol/openid-connect/revoke`
(`KeycloakTokenRevoker`) before touching local storage:

- 2xx: revoked. Clear and end the session.
- 4xx: Keycloak was reached and refused. The token is already dead or
  unusable, and end-session ends the whole SSO session (and every token in it)
  anyway, so we log it, clear and end the session.
- Transport failure, a stall or 5xx: Keycloak is unreachable. Nothing is
  cleared, and the user stays signed in and is told so. The end-session
  redirect would fail the same way, and an un-revoked token must not be left
  behind on a device the user believes is signed out.

**Sign-out needs a connection.** With `IConnectivity` offline, the Sign out
button is disabled and the Account card says the device can't be signed out
securely until it connects. There is no deferred "revoke on next contact":
the device the user walked away from would still hold a live token.

**Sign-out clears the outbox, after asking.** When the outbox holds pending
commands, a `ConfirmDialog` says how many unsynced changes will be removed.
Cancelling changes nothing. Confirming clears the replica, the outbox, the
statistics cache, the cached public snapshots (their tokens are bearer
capabilities) and the session, then leaves for end-session. That is the same
per-user set `ReplicaOwnership` purges on a user switch.

## Considered alternatives

- **Revoke on next contact when offline** (the issue's proposal). The token
  stays live and readable on the device for an unbounded time, and nothing
  runs once the user is signed out locally. The maintainer rejected it.
- **Keep the outbox across sign-out** (ADR-0028 and earlier). It keeps
  another user's unsent task content readable on a shared device. The
  confirmation dialog leaves the decision with the person who wrote it.
- **Clear locally even when Keycloak is unreachable.** This looks like a
  sign-out but leaves a live refresh token and SSO session, which is exactly
  the issue.
- **Set `post.logout.redirect.uris` explicitly in the realm import.** Not
  needed: Keycloak 26 falls back to `redirectUris`, and a realm-import change
  never reaches existing installs.

## Consequences

A shared device keeps nothing of a signed-out user: no replica, outbox,
statistics, opened snapshots, session or OIDC credentials, and no live refresh token or SSO session at
Keycloak.

Signing out offline is no longer possible. A user who must leave a device
while offline has to reconnect first. That is deliberate, but it is a
behaviour change.

Sessions stored before this change have no id token. Until their first
refresh they end the session without a hint, which can show Keycloak's
confirmation page. The refresh token is still revoked first.

A refresh already in flight when sign-out clears storage can write its
rotated session back. That token belongs to the session end-session just
killed, so the next boot's refresh gets `invalid_grant` and clears it (as
ADR-0027 describes).

The trust-window expiry and an `invalid_grant` clear are not explicit
sign-outs. They still keep the outbox; `ReplicaOwnership` drops it when a
different user signs in. Account deletion (`LocalAccountDeletion`) still
leaves the statistics and snapshot caches behind; it is owned by another
change in flight and is not touched here.
