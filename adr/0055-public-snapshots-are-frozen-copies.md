---
title: Public snapshots are frozen copies, served from a new Sharing module
tags: [architecture, modularity, domain, persistence, security]
date: 2026-10-01
status: Active
---

# ADR-0055: Public snapshots are frozen copies, served from a new Sharing module

## Context

`specs/sharing-spec.md` (#104) asks for a second way to share a list: a link
anyone can open with no account, until an expiry the owner picks — the
"gift list" case, where the other side is not a PSPad user at all and never
will be. Visitors need to tick a task, step or reference item as done, and
the owner needs to see those ticks as chips on their own copy, without the
tick ever becoming a completion someone else caused.

Three things stood in the way of reusing member sharing (`adr/0053`) for
this. First, an anonymous visitor has no `userId` — `ListAccess` and every
sync filter key off one, and there is no account to admit. Second, the
content has to survive the owner editing or deleting the list afterwards;
a link to live data would show a visitor whatever the owner typed next,
including nothing at all once something is deleted — the maintainer
rejected a live read-only page for exactly this reason: a snapshot is a
thing you hand someone at a point in time, not a window onto a list that
keeps moving under them. Third, a tick from a page nobody is signed into
has to reach the owner's real task somehow, without teaching `TodoTask` or
`ReferenceItem` that anonymous visitors, tokens or expiry exist.

## Decision

A snapshot is a frozen copy, owned by a new module.

`PSPad.Module.Sharing` holds `ListSnapshot` (the frozen `tasks[]`/`items[]`,
each entry carrying its own `marked`/`markedAt`), `SnapshotBuilder.Build`
(pure: list + tasks + items + now + expiry → a `ListSnapshot`),
`SnapshotPublishing` (publish, revoke, list active), `SnapshotMarking`
(apply a visitor's tick to the frozen copy, then drive Tasks), and
`SnapshotVisit` (a signed-in visitor's own record of having opened one).
It references `PSPad.Abstractions` and `PSPad.Module.Tasks` only — the
second module-to-module edge, same direction and reason as Statistics'
(`adr/0037`): it pattern-matches on `TaskList`, `TodoTask` and
`ReferenceItem` shapes to build the copy, so a rename breaks the build
rather than a mismatch surfacing at render time. `Tasks` never references
`Sharing`; `Sharing` never references `Statistics`, `Presentation`,
`Infrastructure`, MongoDB or ASP.NET Core — all four guarded.

Publishing is online-only HTTP, not a command: `POST /api/lists/{id}/snapshots`
runs straight through `SnapshotPublishing` against server state, the same
shape as `PUT /api/me/timezone`. A snapshot is never synced and never
enters the event log — it is useless to an offline client (nobody reads a
link on a device that only has its own replica) and useless as history
(its whole point is to go stale and vanish on expiry, which `events` never
does). The token is 18 random bytes from `RandomNumberGenerator`,
base64url-encoded, generated server-side and distinct from the snapshot's
own `_id` — guessable ids are fine because they name nothing a visitor can
reach without the token too. Expiry is accepted in `(now, now + 365 days]`;
a custom date is end of that day in the owner's own time zone, not UTC
midnight, so "this Friday" means the owner's Friday. `list_snapshots` and
`snapshot_visits` both carry a TTL index on `expiresAt`
(`expireAfterSeconds: 0`); because TTL deletion runs on its own background
sweep and lags the instant, every read also checks `expiresAt > now`
itself rather than trusting the collection to already be empty. An unknown
token and an expired one return the same 404 — nothing distinguishes "never
existed" from "existed once" to a visitor holding a dead link.

A visitor's tick reaches the owner through the same server-only-command
door `adr/0053` opened for `JoinTaskList`: `SnapshotMarking` saves the mark
on the frozen copy first (so every visitor sees it immediately, even if the
real task is gone), then runs `MarkTaskFromSnapshot` or
`MarkReferenceItemFromSnapshot` — both `IServerOnlyCommand`, excluded from
`/api/commands` — through the normal handler and unit of work, stamped with
the owner as `UserId` and a fresh `CommandId`. A rejection (the task was
deleted since) is logged and dropped; the snapshot's own tick still stands,
because the visitor's page is not wrong just because the owner's side moved
on. `ClearTaskSnapshotMarks` / `ClearReferenceItemSnapshotMarks` are
ordinary, owner-or-member commands that dismiss every chip a task or item
carries — they touch only Tasks' own documents, never the snapshot, so a
dismissed chip does not erase a visitor's tick.

The owner sees new chips on their next sync; pushing them live is issue
#105's own spec, not this one's.

## Considered alternatives

- **A live, read-only page reading current list state.** Simplest to build —
  no frozen copy, no staleness to reason about. Rejected by the maintainer:
  the whole appeal of a snapshot link is handing someone a fixed thing at a
  point in time ("here's the packing list as of today"), not a window that
  silently reflects every edit and deletion afterwards; a visitor mid-read
  seeing a task vanish under them is worse than a page that goes stale on a
  schedule the owner chose.
- **Snapshots as ordinary Tasks commands and aggregates.** Keeps everything
  inside one module and one event log, matching every other piece of state.
  Rejected: a snapshot syncs to no device and is read by nobody who has a
  replica — teaching the sync protocol, the offline outbox and delta pull
  about a document kind that is useless offline buys nothing and costs a
  second kind of exception to every one of those paths.
- **Tasks subscribing to a Sharing-published event when a visitor ticks
  something.** Would let `MarkTaskFromSnapshot` disappear in favor of Tasks
  reacting to "a snapshot entry was marked." Rejected: it reverses the one
  module edge this ADR establishes — Sharing would still need to read
  Tasks' shapes to build the copy, so Tasks would end up depending back on
  Sharing's event types too, and the dependency graph gains a cycle in
  spirit even if not in the `.csproj` references.

## Consequences

A second module-to-module edge exists (Sharing → Tasks), guarded the same
way as Statistics → Tasks — a third such edge should reuse this shape by
default rather than inventing a fourth. Snapshot content can go stale
relative to the live list between publish and open — deliberate, not a bug
to chase. Unknown and expired tokens are indistinguishable by design, which
means a support conversation about "my link stopped working" can never
confirm whether it merely expired or was mistyped. The owner's own chips lag
one sync behind a visitor's tick, same as every other cross-device change
in this system until #105 changes that for this one path. Account deletion's
generic `userId` sweep already covers `list_snapshots` and `snapshot_visits`
with no code change, because both carry `userId` like every other
collection — only `tasklists.Members`, a nested array, needed the explicit
`$pull` `adr/0053` added.
