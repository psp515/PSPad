---
title: Account deletion bypasses the command pipeline for a generic cross-collection wipe
tags: [identity, persistence, architecture, security]
date: 2026-09-24
status: Active
---

# ADR-0033: Account deletion bypasses the command pipeline for a generic cross-collection wipe

## Context

Issue #23 asks for a way to remove an account: all of the user's data, and
the Keycloak user itself. AD-2/AD-3 model every write in the system as a
shared `ICommand` that a handler loads, decides against one aggregate,
applies, and stages into one transaction, replayable offline through the
same handler client- and server-side. Account deletion doesn't fit that
shape on any of three counts: no single aggregate owns "all of this user's
data" (it spans `User`, every `Area`, `TaskList`, `TodoTask`, `Goal`, the
`Inbox`, and the event log itself); it cannot run offline or through the
outbox, because queuing "delete everything" to replay later contradicts the
very data the replica needs to keep functioning until it does; and there is
no reader left to ever see a resulting History event, since the account
producing it is gone in the same operation.

It also has to reach an external system Mongo commands never touch —
Keycloak — to remove the login itself, and the issue's request for a
password-confirmation popup runs into a real constraint: this app has no
local password store (AGENTS.md §4, settled). Keycloak is the only identity
provider; there is no password of ours to check.

## Decision

**Account deletion is one non-command endpoint, `DELETE /api/account`,
handled directly in `PSPad.Api`, not through `ICommandHandler<T>`.**

1. In one MongoDB transaction, enumerate every collection in the database
   (`Database.ListCollectionNames()`) and run `DeleteMany({ userId: id })`
   against each, `id` taken from the caller's validated JWT `sub` — never
   from client input. This covers `events` and `processed_commands` too:
   nothing about this user is retained anywhere.
2. Only once that transaction commits, call Keycloak's Admin REST API to
   delete the Keycloak user, authenticating with the bootstrap master-realm
   admin credentials the compose stack already provisions
   (`KEYCLOAK_ADMIN_USER`/`KEYCLOAK_ADMIN_PASSWORD` in `docker/.env.example`).
   Mongo first, Keycloak second — see alternatives for why.
3. The client confirms intent by having the user type their own email
   address (type-to-confirm), not a password — there isn't one. This is a
   misclick guard, not the authorization boundary; the JWT is. The delete
   call is awaited inline in the confirmation dialog (spinner, no redirect),
   requires connectivity, and on success the client deletes its entire
   IndexedDB database and the `oidc.*` `sessionStorage` keys before landing
   on `/welcome`.

## Considered alternatives

- **Model it as `ICommand<DeleteAccount>` through the standard pipeline** —
  rejected. No aggregate owns cross-cutting "all of this user's data", it
  cannot be queued in the offline outbox without the queued intent
  outliving the data it depends on, and a History event for it would have
  no future reader.
- **Hardcode each module's collection name in the delete handler** —
  rejected. It would put `PSPad.Api`'s account-deletion code in the
  business of knowing Tasks/History/Identity's storage shape, and would
  need a manual update for every future aggregate (habits, yearly goals,
  per AGENTS.md §9). The generic `ListCollectionNames()` sweep, filtered by
  `userId`, costs nothing today and needs no maintenance as the schema
  grows.
- **A new Keycloak confidential client with a service-account and a
  `realm-management` delete-users role** — rejected. That is a permanent,
  always-available credential capable of deleting any user in the realm,
  provisioned for one narrow, rarely-used operation. The bootstrap master
  admin already exists in every deployment; reusing it needs zero new realm
  configuration.
- **Keycloak step-up re-authentication (`prompt=login` redirect) as the
  confirmation gate** — rejected. It proves identity against the real
  password store (Keycloak's own), but it leaves the app and returns via a
  full redirect, which contradicts the inline, awaited-in-the-popup flow
  this feature is meant to have, for a disproportionate cost given this
  app's self-hosted, single-owner threat model.
- **Delete the Keycloak user before the Mongo data** — rejected. If the
  Mongo transaction then failed, the person would be locked out of an
  account whose data still exists, with no way back in to ever delete it.
  Mongo first means the worst failure mode is an orphaned, empty Keycloak
  login an administrator can remove by hand — never surviving personal data
  with no owner left to request its own deletion.

## Consequences

Every future aggregate is covered by account deletion with no code change
here — the sweep is generic, not a checklist that silently goes stale. Only
one narrow, clearly-reasoned operation in the codebase bypasses the
command/event pattern, and the two disqualifying properties (spans every
aggregate; cannot run offline) are exactly the ones that make the standard
pipeline the wrong fit rather than an inconvenient one.

This is the first place application code — not just Keycloak's own
bootstrap — uses the master admin credential. A bug that resolves the
target user from anything other than the caller's own validated JWT would
be able to delete an arbitrary account; the handler must never accept a
target id as input. If the Keycloak call fails after the Mongo transaction
has already committed, there is no automatic retry beyond a couple of
immediate attempts and no reconciliation job — an administrator has to
notice and remove the orphaned login manually. Deletion also has a hard
online requirement with no offline story, unlike every other write in the
system.
