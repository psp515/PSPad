---
title: Statistics writes one record for the owner and one for the actor, versioned for a clean rebuild
tags: [architecture, persistence, analytics, sync, identity]
date: 2026-10-01
status: Active — amends ADR-0037
---

# ADR-0054: Statistics writes one record for the owner and one for the actor, versioned for a clean rebuild

## Context

`adr/0053` split every domain event's `UserId` (the aggregate's owner) from
`ActorId` (who actually issued the command). Before sharing, the two were
always the same person, and `StatisticsRecord` carried a single `UserId` —
whoever's history the record belonged to. Once a member can complete the
owner's task, that assumption breaks: the owner's feed and charts must still
show the completion (it is their task), but the member who did the work also
expects to see it in their own history — "what have I been doing" should not
go blank just because the task lives in someone else's list.

`statistics_records` keyed its `_id` on the event's `seq` alone, one row per
event. Giving the actor their own copy of the record needs a second row for
the same event, which collides with that key — `_id` has to carry whose
history the row belongs to, not just which event produced it.

Outstanding-open and by-goal both ride running totals derived from
`statistics_records`. A member's completion of the owner's task must not
drive the member's own "currently open" count negative, and a task's goal
is the owner's business, invisible to a member who never created it.

Every `statistics_records`/`statistics_labels` row written before this
change has no `role` field at all, and `ILabelStore.AllAsync(userId)` cannot
resolve a label that belongs to a different user's `userId` — exactly the
case a member's feed now needs. Both gaps mean existing data needs
reshaping, not just new writes going forward correctly.

## Decision

**Two records per event when owner and actor differ.** `ActorId == UserId`
still writes one record, as before. Otherwise `StatisticsRecordProjection`
writes the owner's record (`Role: Owner`, unchanged shape) and a second
record for the actor (`Role: Actor`): same `Seq`, `UserId` set to the actor,
`GoalId` cleared, `CompletionNumber` recomputed against the actor's own
completion count for that task (`IStatisticsStore.CountCompletionsBeforeAsync(actor, ...)`),
everything else copied. `statistics_inbox_records` is untouched — the Inbox
is never shared, so it never needs an actor copy.

**Record identity carries who it is for, not just which event produced
it.** `StatisticsRecord.Id` becomes `"{seq}:{userId:N}"`
(`StatisticsRecord.IdFor`), and the record gains an explicit `Seq` field
since `Id` is no longer the bare sequence number.
`StatisticsRecordView.Id` on the wire stays a `long` carrying `Seq` — the
client's `before=` paging is unchanged, because it only ever saw the
sequence number, never the composite id.

**Labels resolve by id, not by the caller's `userId`.** `ILabelStore`
gains `ByIdsAsync(ids)`. `StatisticsReader` collects every `ListId`/`GoalId`
on the page it is about to render and resolves them that way, so a member's
feed can show the name of a list they do not own — the label was saved
under the owner's `userId`, which `AllAsync(callerId)` could never have
found. `StatisticsOverviewReader`'s by-goal chart keeps `AllAsync(userId)`:
goals are the owner's, an actor record never carries a `GoalId`, and the
chart has no cross-owner id to resolve.

**Outstanding and NetChange stay owner-only; ByGoal counts Owner records
only.** `StatisticsCharts.Outstanding` already filtered to `Role: Owner`
records before this ADR (`adr/0037`'s lifecycle tracking); this decision
keeps that filter and extends the same reasoning to the tiles built from
it. `NetChange` is `Outstanding`'s last point minus its first — since
`Outstanding` only ever counts the owner's open tasks, a member's net
change is 0 by definition, and showing a line that can never move for a
member would be worse than not showing a role split there at all. `ByGoal`
restricts to `Role: Owner` records too, independent of the `GoalId: null`
on actor records — belt and suspenders, since a future record kind could
in principle carry a `GoalId` on an actor copy by mistake.

**A versioned projection rebuilds once instead of migrating in place.**
`statistics_state` gains `projectionVersion` (absent = 1, implicit from
before this change). `StatisticsProjection.Version = 2`.
`IProjectionMarker.AdoptVersionAsync(version)` compares the stored version
to the code's; when the stored version is older, it resets
`lastProcessedSeq` to 0 and records the new version, and
`StatisticsReplay.ReplayAsync` responds to a true result by calling
`IStatisticsReset.ClearAsync` — which drops `statistics_records`,
`statistics_inbox_records` and `statistics_labels` and recreates their
indexes — before reading from the (now zero) marker. The whole log replays
through the unchanged `adr/0036` path and reconstructs every row with
`Role` populated correctly. This is not an audit-table migration script: it
is the same "rebuildable from `events` alone" property `adr/0037` already
claimed for this data, exercised for the first time. The marker is reset
before the collections are dropped, not after, so a crash between the two
steps leaves a zero marker and the next start replays over whatever
survived — `SaveAsync` upserts by `Id`, so a partial replay converges
rather than duplicating rows.

## Considered alternatives

- **Write only the actor's record, drop the owner's.** Rejected: the owner
  did not do the work, but it is still their task and their history — an
  owner who never shares anything would see nothing change, but an owner
  whose member completes something for them would lose it from their own
  feed and charts entirely. The whole point of sharing is that the owner's
  side keeps working as before.
- **Write only the owner's record, no actor copy.** Rejected: this is what
  shipped before `adr/0053` made the owner/actor distinction possible, and
  it is the gap this ADR exists to close — a member's own statistics screen
  would never show work they did on a shared list, which reads as "my
  completions vanished" from the member's point of view.
- **One record carrying both a `userId` and an `actorId`, queried twice.**
  Keeps one row per event, matching `adr/0037`'s original shape most
  closely. Rejected: every per-user index and query (`{userId: 1, seq: -1}`
  for the feed, the completion counter, the lifecycle scan for
  `Outstanding`) assumes exactly one user field to filter on; querying "is
  this user the owner or the actor of this row" doubles every filter and
  makes the record itself ambiguous about whose history it belongs to. Two
  rows, each unambiguously owned by one user, is cheaper than teaching every
  reader to ask "which field do I match on this time."

## Consequences

A shared task's completion costs a second write and, going forward, a
second row per user who ever interacts with it — paid only on sharing's
actual usage, not on every event the way `adr/0053`'s `ActorId` field is.
The first start after this deploy replays the entire event log once, which
is slow in proportion to how much history exists and briefly leaves
statistics endpoints answering from an empty projection while it runs — an
accepted, one-time cost rather than a bespoke migration script that itself
needs testing and could drift from what replay actually produces. Feed
paging (`before=`) now walks by `Seq`, not by the raw `_id` the record is
stored under, which is already how `StatisticsRecordView.Id` was read by
the client — no client change. A member's by-goal chart is permanently
empty for records on a shared list, because goals do not become the
member's business just because they completed the owner's task — if a
future design decision wants a member to see goal progress on lists they
work on, that is a new decision, not something this ADR's `GoalId: null`
accidentally enables.

## Amendment (2026-10-01, final review)

The order is reversed: `StatisticsReplay` asks `IProjectionMarker.IsBehindAsync`,
clears, and only then calls `AdoptVersionAsync` (now a plain write). Adopting
first meant a failed drop — or a host dying between the two — left the new
version recorded over the old rows, so the next start replayed from zero on
top of them and doubled every count. Clearing is idempotent, so retrying it
on the next start is safe.
