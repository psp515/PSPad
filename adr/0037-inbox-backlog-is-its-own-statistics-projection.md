---
title: The Inbox backlog is its own projection, its own collection, and a running level seeded with an opening set
tags: [architecture, persistence, analytics, ui]
date: 2026-09-25
status: Active
---

# ADR-0037: The Inbox backlog is its own projection, its own collection, and a running level seeded with an opening set

## Context

The statistics screen answers "what got done" but says nothing about what
was captured and then left to rot. In GTD the Inbox is a queue that is
supposed to be emptied; a capture still sitting there weeks later is the
failure mode the Inbox exists to expose, and nothing in the module projected
inbox events at all — `statistics_records` only knew about tasks.

Two shapes were open. Where do inbox facts live, given `StatisticsRecord` is
task-shaped (`TaskId`, `TaskName`, `ListId`, `GoalId`, `CompletionNumber`,
plus `ITaskSnapshotSource` status resolution for the feed)? And how is
"still waiting at week's end" counted, given the module had already been
bitten twice by treating a *level* as a windowed *tally* —
`StatisticsCharts.Outstanding` and `IStatisticsStore.OpenTaskIdsBeforeAsync`
exist precisely because a count cannot express *which* items were already
open before the window began.

## Decision

We will project inbox events into their own collection and derive the
backlog as a running level seeded with an opening set.

- `InboxRecordProjection : IDomainEventHandler` maps `InboxItemCaptured`,
  `InboxItemOrganised` and `InboxItemDiscarded` to an `InboxRecord`
  (`Id` = the event's `seq`, `UserId`, `At`, `Kind`, `ItemId`) in
  `statistics_inbox_records`, read through `IInboxRecordStore`. Keyed by
  `seq` like every other statistics row, so a replay or a duplicate dispatch
  upserts rather than duplicates. It is registered alongside the existing
  two handlers, so the startup replay covers it with no further plumbing.
- `StatisticsCharts.InboxBacklog(records, today, days, zone, heldAtStart)`
  returns one `WeeklyCount` per week the window touches, counting the items
  **still held when that week ended** — the current week measured at today,
  not at its Saturday. Weeks start Sunday, matching the consistency grid's
  rows. State is per item, never a signed sum: `Captured` adds the id,
  `Organised` and `Discarded` remove it, removing an absent id is a no-op,
  and records are applied in `(bucketed day, Id)` order for the same
  offline-commit reason `Outstanding` uses.
- `StatisticsOverviewReader` passes `IInboxRecordStore.SinceAsync` and
  `HeldItemIdsBeforeAsync` the *same* instant it already passes
  `SinceAsync`/`OpenTaskIdsBeforeAsync`, so no record is both charted and
  part of the opening balance.
- The screen renders it as a second heatmap in the first one's visual
  language, one cell per week, with its own shade key and per-cell
  `title`/`aria-label`.

## Considered alternatives

- **Fold inbox events into `StatisticsRecord`** with new `RecordKind`s and
  the `ItemId` in `TaskId`. Rejected: the record feed pages every kind for a
  user and resolves each row's current task status, so inbox rows would
  appear in the feed as tasks that never existed — and every existing
  chart's kind filter would need re-auditing on an open PR. A separate
  collection touches nothing already shipped, at the cost of one more
  projection, one more store and one more index.
- **Count captures per week (how many were captured that week)** rather than
  how many survived it. Rejected because it answers a different question:
  capture volume is not backlog, and an item captured in January and still
  waiting in September would count once, in January, exactly when it was not
  yet a problem.
- **Count only records inside the window, with no opening set.** Rejected:
  this is the bug `Outstanding` already had. Every early week would
  under-report, because the backlog that predates the window would simply
  vanish — and the 365-day range would disagree with the 30-day range about
  the same week.
- **Derive the backlog from the `Inbox` aggregate's current items.**
  Rejected for the reason `adr/0035` gives: current state cannot say what was
  true at the end of a past week, and an item organised last month would
  erase its own history.
- **A weekly grain that follows the user's week start setting.** Rejected as
  premature — there is no such setting, and the consistency grid already
  pins Sunday-first rows. If one ever lands, both grids move together.

## Consequences

The backlog is honest across a window boundary, and it is cheap: one extra
projection over events the log already holds, rebuildable from seq 0 like
every other statistics row. Habits and any future "queue age" chart get the
same per-item state for free.

The costs are real. Statistics now owns four collections rather than three
(`adr/0035` said three), and the module has two record shapes a reader must
choose between. `HeldItemIdsBeforeAsync` folds every record older than the
window on each overview request, exactly as `OpenTaskIdsBeforeAsync` does —
linear in a user's lifetime inbox history, and the same future problem, to be
solved for both at once (a periodic snapshot) rather than twice separately.
The weekly grain means a captured-and-organised-within-the-week item is
invisible: the chart shows survival, not throughput, which is the intent but
will read as "nothing happened" on a busy week that was fully cleared.
`StatisticsOverview` gaining a field also invalidates the client's cached
payload, so the `localStorage` key carries a shape version now — an overview
cached under the old key is ignored and re-fetched rather than rendering a
missing series as null.
