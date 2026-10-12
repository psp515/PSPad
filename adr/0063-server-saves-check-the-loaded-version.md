---
title: Server saves check the loaded version, and sharing and membership go online-only
tags: [architecture, storage, sync, security, sharing]
date: 2026-10-12
status: Active
---

# ADR-0063: Server saves check the loaded version, and sharing and membership go online-only

> Status: Active (amends [ADR-0005](0005-offline-conflicts-last-write-wins.md))

## Context

Issue #131. `MongoUnitOfWork` replaced each staged aggregate by `_id` alone.
Handlers load the aggregate before the transaction starts, so a request that
read a `TaskList` before the owner removed a member, or stopped sharing, could
commit after and write the stale document back: the member and the invite
returned. Parallel wrong invite codes each wrote `WrongCodes = n + 1`, so the
five-try lockout could be exceeded. ADR-0005's last-write-wins was meant for
one person's two devices replaying offline commands, not for two server
requests silently overwriting each other's committed changes.

Sharing a list, ending a link, removing a member and leaving a list also went
through the offline outbox, so a removal could sit queued on a phone while
the removed member kept full access.

## Decision

We will make every server save conditional on the version the handler
loaded: the replace filter is `_id == id && version == loaded`, where
`loaded` is the aggregate's version minus the events staged with it, and a
new aggregate is inserted only if no document has that id. A miss, or
MongoDB's transient write conflict between two transactions, throws
`ConcurrencyConflictException`; `CommandDispatcher` runs the command again
against freshly loaded documents, after a short jittered pause, up to five
attempts in all, and then
rejects it with "Someone else changed this at the same time. Try again." The
whole transaction, including the processed-command marker, aborts on a miss,
so each retry is a clean re-run.

We will move `ShareTaskList`, `StopSharingTaskList`, `RemoveListMember` and
`LeaveTaskList` off the outbox. They become `IServerOnlyCommand`s, reachable
only through `POST /api/lists/{id}/share`, `DELETE /api/lists/{id}/share`,
`DELETE /api/lists/{id}/members/{memberId}` and `POST /api/lists/{id}/leave`,
the same way joining already works. The client calls them online and syncs
after a success; offline, the controls are disabled.

Offline conflicts are still last-write-wins (ADR-0005): a queued command that
lands after another device's change is replayed onto the fresh document, not
rejected for being stale.

## Considered alternatives

- **Atomic `$pull` / `$unset` / `$inc` for membership and invite fields
  only** — closes the specific races, but every other whole-document replace
  (rename, move, a wrong-code join) would still write stale membership back
  unless those fields were excluded from every replace; a version check
  fixes the cause once for all aggregates.
- **Version check on `TaskList` only** — the mechanism is the same cost for
  every aggregate, and lost updates on tasks from two devices syncing at the
  same moment are the same bug in a milder form.
- **Reject on conflict without a retry** — would surface spurious rejections
  for commands that are perfectly valid on the fresh document, contradicting
  ADR-0005's "replay onto the latest state" intent.
- **A single retry** — five parallel wrong invite codes collide more than
  once; a rejected attempt would go uncounted and the lockout could be dodged.
- **Keep sharing commands in the outbox** — a removal must take effect when
  the owner taps it, not when their phone next has signal.

## Consequences

Concurrent server writes to one aggregate can no longer lose updates, and the
wrong-code lockout counts every attempt. The cost is one extra filter field
per save and, under real contention, extra handler runs. It also turns the
transient write conflicts concurrent commands already hit (every transaction
increments the shared sequence counter) from a 500 into a retry. A document whose
stored `version` was changed outside the unit of work would make every save
to it miss on every attempt and be rejected; nothing does that today, and a migration
that does must bump `version` consistently. Sharing and membership now need a
connection, and an old client's queued sharing commands are answered as
unrecoverable and dropped (ADR-0021).
