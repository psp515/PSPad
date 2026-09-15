---
title: Purge the local replica and outbox when the signed-in user changes
tags: [offline, sync, identity]
date: 2026-09-15
status: Active
---

# ADR-0018: Purge the local replica and outbox when the signed-in user changes

## Context

The client's IndexedDB (`pspad`) is one database per browser profile, not one
per user. `documents` are already scoped by a `[type, userId]` index, so a
second user signing into the same browser only ever reads their own rows back
out of it. The `outbox` store carries no such scoping — it is a flat, ordered
queue of not-yet-pushed commands.

`SyncService.PushAsync` stops at the first rejected command and keeps
everything after it queued, on the reasoning that a later command usually
depends on the one that failed (AD-5: rejections are surfaced, never dropped
silently). That reasoning assumes rejections are transient or actionable. A
command left in the outbox by a user who signs out before it syncs — offline,
or the tab closed — is neither: if a different person then signs into the
same browser, `CommandDispatcher` rejects it forever with "That command is
for a different user," and because it sits at the head of the queue, it
blocks every command behind it and re-fires the rejection on every sync poll
(every 60s) until the browser's storage is manually cleared.

Nothing in AD-5 or AD-6 spoke to multi-user-per-device; both were written
assuming a session's replica belongs to the user it was built for.

## Decision

We will record which user a browser's local replica currently belongs to
(a plain value in the existing `meta` store), and check it once at sign-in.
When the signed-in user differs from the recorded owner, we wipe `documents`,
`meta` and `outbox` before doing anything else — no attempt to salvage or
re-attribute what's there. The new user then starts from an empty replica,
exactly as AD-6 already treats a fresh replica: disposable, server is truth,
rebuilt by the next pull.

This is `PSPad.App.State.ReplicaOwnership`, called from `AppShell` right
after `/api/me` resolves and before `SyncCoordinator.Start()` — so a stale
command never gets a chance to push under the wrong session.

## Considered alternatives

- **Scope the outbox by user id and filter what gets pushed.** Rejected: it
  treats the symptom, not the cause — a previous user's unsynced edits would
  still silently vanish (never retried under their own session on this
  device, never surfaced), which is a worse outcome than the clean slate this
  decision gives the new user. It also leaves `documents` and the sync marker
  mixed across users in the same database for no benefit, since they're
  already scoped or global respectively.
- **Never let two users share a browser profile — out of scope.** Rejected:
  self-hosted, multi-user, shared-hardware use (a family tablet, a shared
  workstation) is exactly the scenario AGENTS.md §4 describes PSPad
  targeting; treating it as unsupported would just mean the bug resurfaces
  as a support request instead of a design decision.
- **Detect the mismatch server-side and have the API tell the client to
  wipe.** Rejected: adds a round trip and a new contract for something the
  client can already tell from its own `/api/me` response; AD-6 already puts
  the client in charge of its own replica lifecycle.

## Consequences

A user switch on shared hardware costs one full resync instead of a silently
broken outbox. A user who signs out with unsynced local edits still pending
loses them if a different person signs in before they return — no different
in outcome from AD-5's existing stance that offline conflicts are
last-write-wins per aggregate, just applied at the point of a user switch
rather than a concurrent edit. `IReplica` and `IOutbox` both grow one new
member (`ClearAsync`, plus `OwnerAsync`/`SetOwnerAsync` on `IReplica`) that
every implementation — real and the in-memory test fakes — must carry.
