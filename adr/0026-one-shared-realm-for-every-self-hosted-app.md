---
title: One shared Keycloak realm for every self-hosted application
tags: [identity, deployment, architecture]
date: 2026-09-21
status: Active
---

# ADR-0026: One shared Keycloak realm for every self-hosted application

## Context

The bundled realm was named `pspad` and its clients `pspad-app` and
`pspad-api`, on the assumption that the realm belonged to PSPad. It does not
scale past one application: a second self-hosted application would either get
its own realm — a separate account list, separate signing keys, no single
sign-on, a second set of credentials for the same person — or squat inside a
realm named after a different product.

The realm also carried a third client, `pspad-client`, referenced nowhere in
the repository: a public client with `http://localhost:5000/*` as an open
redirect target.

[ADR-0015](0015-one-self-host-compose-stack.md) put Keycloak in PSPad's own
compose stack. That stays true for now, but it makes the realm's lifetime
PSPad's lifetime, which is the wrong ownership once other applications rely
on the same accounts.

The constraint that shapes everything else: PSPad's user id is derived from
the token's `sub` claim (`src/PSPad.Api/Identity/CurrentUser.cs`), the
Keycloak user's UUID. That UUID is a property of the user record. It survives
a realm rename, a client rename and any number of new clients — but a second
realm, or a recreated `keycloak-data` volume, mints a new one and orphans
every document the old account wrote.

## Decision

We will run one Keycloak realm, `psplace`, holding the accounts for every
self-hosted application, and give each application two clients named
`<app>-frontend` (public, PKCE `S256`) and `<app>-backend` (bearer-only).
PSPad's are `pspad-frontend` and `pspad-backend`. Each frontend client gets
an audience mapper for its own backend and no other. `pspad-client` is
deleted.

Adding an application means adding clients in the admin console, never
adding a realm and never re-importing `realm-psplace.json` —
`docker/keycloak/README.md` is the procedure of record, including the
in-place rename path that preserves `sub` for an existing installation.

## Considered alternatives

- **A realm per application.** Hard isolation, which is the point of a realm —
  but it is isolation we do not want here. No single sign-on, one account per
  application per person, and a separate key rotation and federation
  configuration each time. Correct for multi-tenant, wrong for one operator's
  own applications.
- **Keep the realm named `pspad` and add other applications' clients to it.**
  Cheapest edit, zero migration. Rejected because the name would lie about
  ownership: the first thing a second application's operator reads is that
  they are authenticating against somebody else's product.
- **Extract Keycloak into its own compose stack now.** The right end state
  once a second application exists — the realm's lifetime should not be
  PSPad's. Deferred rather than rejected: PSPad is still the only consumer,
  `.env.example` already documents pointing `KEYCLOAK_INTERNAL_AUTHORITY` at
  an external Keycloak, and splitting the stack changes what every existing
  self-hoster runs for no benefit they can use yet.
- **Generate `realm-psplace.json` from `.env` at container start.** Would fix
  the hard-coded `localhost` redirect URIs. Rejected as out of scope here, and
  weak in general: the file is read only on the first start, so generating it
  helps exactly once and then diverges from the live realm.

## Consequences

Single sign-on across applications comes free, and a person has one account
and one password policy. Adding an application touches no existing client and
no user.

Existing self-hosters must act: the realm and both client names change, so
`docker/.env` needs four values updated and the realm needs renaming in the
admin console. Doing it with `down -v` instead destroys their data — the
README leads with that warning, and it is the main cost of this decision. In
exchange for that one-time step, later applications cost no migration at all.

The realm still lives in PSPad's compose stack, so `docker compose down -v`
run against PSPad destroys other applications' accounts too. That is a real
hazard for as long as the split in the third alternative is deferred, and the
reason to revisit it as soon as a second application is real.

Because everything shares a realm, a compromise of the Keycloak instance is a
compromise of every application at once, and realm-level roles are visible to
all of them. Per-application authorization therefore belongs in client roles,
not realm roles, and every client ships with `fullScopeAllowed` off so a
token cannot carry another application's roles by default.
