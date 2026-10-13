---
title: Account deletion needs a sign-in from the last five minutes
tags: [identity, security, http]
date: 2026-10-13
status: Active
---

# ADR-0066: Account deletion needs a sign-in from the last five minutes

> Amends [ADR-0034](0034-account-deletion-bypasses-the-command-pipeline.md).

## Context

`DELETE /api/account` wiped every document a user owned and their Keycloak
login on any valid access token (OWASP A07, issue #134). The client keeps a
refresh token for up to seven days and mints access tokens from it silently,
so a stolen token, a briefly unlocked device or an XSS payload could delete
an account without ever meeting the password or the second factor. The
typed-email confirmation is a misclick guard, not an authorization boundary.

## Decision

We will require a recent sign-in for account deletion, using the OAuth 2.0
Step-Up Authentication Challenge (RFC 9470):

- The API reads the access token's `auth_time` claim. When it is missing,
  unreadable or older than `AccountDeletion:MaxAuthenticationAge` (code
  default 5 minutes), the endpoint deletes nothing and answers `401` with
  `WWW-Authenticate: Bearer error="insufficient_user_authentication",
  error_description="…", max_age=<seconds>`. The check runs before the
  projection drain and the wipe.
- CORS exposes `WWW-Authenticate`, so the browser client can read it.
- The client maps that challenge to `AccountDeletionOutcome.ReauthenticationRequired`.
  The delete dialog then offers "Sign in again", which starts an interactive
  OIDC sign-in with `prompt=login` and `max_age=0` and returns to
  `/settings?deleteAccount=confirm`. Settings strips the query and reopens the
  delete dialog; the person types their email again and the retry carries a
  token whose `auth_time` is the new sign-in.
- Keycloak keeps `auth_time` on the user session and moves it forward on
  re-authentication, so tokens refreshed after the step-up carry the fresh
  value. The realm's default `basic` client scope (Keycloak 25+) maps it into
  access tokens.

## Considered alternatives

- **`403` instead of `401`** — `403` says the caller may never do this;
  RFC 9470 defines `401` with `insufficient_user_authentication` for exactly
  "sign in again and retry", and the client's ordinary error path stays for
  every other failure.
- **Always force a fresh sign-in before the delete call** — every deletion
  would leave the app for Keycloak even right after a sign-in, and the server
  would still need the check, since the client is not the boundary.
- **Retry the delete automatically after the sign-in** — a crafted link to
  the return URL would then delete the account behind one password prompt
  the person may not connect to a deletion. Reopening the dialog keeps the
  typed confirmation in front of the irreversible step.
- **Ask for the password in the app** — PSPad never sees passwords
  (ADR-0034); Keycloak also enforces the configured second factor.
- **Documented self-hoster setting** — the age is a code default with a
  configuration key, like the rate limits in ADR-0062; nothing in Compose
  sets it.

## Consequences

- A stolen access or refresh token alone can no longer delete an account;
  the attacker also needs the password (and the second factor, if set).
- Deleting an account more than five minutes after signing in costs one
  extra round trip through Keycloak and a second typed confirmation.
- A Keycloak realm whose access tokens lack `auth_time` (for example a realm
  created before Keycloak 25 without the `basic` scope) makes every deletion
  answer the challenge, even right after signing in again. Such a realm must
  add an `auth_time` mapper to the `pspad-frontend` client.
- The check trusts the identity provider's clock against the API's; the
  bundled stack runs both on one host, and a token signed slightly in the
  future counts as recent.
