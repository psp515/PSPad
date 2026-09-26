---
title: Inbox captures are their own statistics projection, counted per week
tags: [architecture, persistence, analytics, ui]
date: 2026-09-25
status: Active (revised 2026-09-26, pre-merge — see "Revision")
---

# ADR-0039: Inbox captures are their own statistics projection, counted per week

## Revision

This record was rewritten on 2026-09-26, before any of it shipped. Its first
version decided the chart was a **running backlog level** — how many captured
items were still waiting at each week's end — seeded with an opening set of
item ids held before the window. The maintainer asked instead for capture
volume: how many items were created in the Inbox each week. That is a
different measure, and it makes the opening set, the per-item add/remove
state and the `Organised`/`Discarded` half of the projection dead machinery,
so keeping the old text would have documented code that no longer exists.

The normal rule (AGENTS.md §11) is that a past decision is never edited —
a new ADR supersedes it. That rule protects decisions that have *shipped*:
something in `main` that someone may be reasoning about or running. This one
lived only on the unmerged `feat/21-statistics-module` branch and never
reached `main`, so there is no history to preserve — only a not-yet-true
description of the branch's own code. Rewriting in place is honest; leaving a
superseded-on-arrival pair for a reader to reconcile is not. Everything below
describes what actually ships.

## Context

The statistics screen answers "what got done" but says nothing about what
comes *in*. In GTD the Inbox is the one funnel every commitment passes
through, so how much lands in it each week is the intake side of the same
story the completion charts tell — and nothing in the module projected inbox
events at all: `statistics_records` only knew about tasks.

Where do inbox facts live, given `StatisticsRecord` is task-shaped
(`TaskId`, `TaskName`, `ListId`, `GoalId`, `CompletionNumber`, plus
`ITaskSnapshotSource` status resolution for the feed)? And what exactly does
the chart count — what arrived, or what survived?

## Decision

We will project `InboxItemCaptured` into its own collection and chart it as a
per-week tally of captures.

- `InboxRecordProjection : IDomainEventHandler` maps `InboxItemCaptured` —
  and nothing else — to an `InboxRecord` (`Id` = the event's `seq`, `UserId`,
  `At`, `ItemId`) in `statistics_inbox_records`, read through
  `IInboxRecordStore` (`SaveAsync`, `SinceAsync`). Keyed by `seq` like every
  other statistics row, so a replay or a duplicate dispatch upserts rather
  than duplicates. It is registered alongside the existing two handlers, so
  the startup replay covers it with no further plumbing.
- `InboxItemOrganised` and `InboxItemDiscarded` are **not** projected. Once
  the chart counts arrivals, what later became of an item changes no number
  on the screen, and a row nothing reads is a write that can only rot.
  Emptying the Inbox is still in the `events` log, where a future metric that
  wants it — time-to-organise, or an organised/discarded split — would
  rebuild it from `seq` 0 like every other statistics row.
- `StatisticsCharts.Captures(records, today, days, zone)` returns one
  `WeeklyCount` per week the window touches, counting the captures whose day
  — bucketed in the **user's** time zone, never machine-local time — falls
  inside both the window and that week. Weeks start Sunday, the same
  convention the consistency grid's Sunday accent uses. The first week of a
  window is partial by construction: it counts only its days from the
  window's first day on, and a capture older than the window is simply not
  read.
- The screen renders it as a `MudChart` bar chart — one bar per week, with a
  `mud-sr-only` table of week/count pairs beside it, since `MudChart` emits
  no per-bar accessible text.

## Considered alternatives

- **Fold inbox events into `StatisticsRecord`** with new `RecordKind`s and
  the `ItemId` in `TaskId`. Rejected: the record feed pages every kind for a
  user and resolves each row's current task status, so inbox rows would
  appear in the feed as tasks that never existed — and every existing
  chart's kind filter would need re-auditing on an open PR. A separate
  collection touches nothing already shipped, at the cost of one more
  projection, one more store and one more index.
- **A running backlog level** — how many captures were still waiting at each
  week's end — which is what this ADR first decided. Rejected by the
  maintainer in favour of intake volume. It is the more expensive measure:
  a level cannot be derived from records inside the window alone, so it needs
  an opening set of item ids (`HeldItemIdsBeforeAsync`, folding every record
  older than the window on each request) and per-item add/remove state, the
  same machinery `Outstanding` needs for open tasks. A tally needs neither.
  It also reads badly on a week that was captured and cleared: survival
  charts "nothing happened" for a busy week that was fully emptied, where a
  tally shows the work.
- **Keep projecting `Organised`/`Discarded` anyway**, in case a later chart
  wants them. Rejected as speculative: nothing reads them, they can be
  rebuilt from `events` whenever something does, and a field or row with no
  consumer misleads the next reader about what the collection is for.
- **Count captures net of same-week emptying** ("how many were still there on
  Sunday night"). Rejected: that is the backlog measure again, with a
  seven-day memory instead of an unbounded one — the worst of both, and it
  would need the removal records back.
- **Derive intake from the `Inbox` aggregate's current items.** Rejected for
  the reason `adr/0037` gives: current state cannot say what was true in a
  past week, and an item organised last month would erase its own history.
- **A weekly grain that follows the user's week start setting.** Rejected as
  premature — there is no such setting, and the consistency grid already
  pins Sunday. If one ever lands, both move together.

## Consequences

The chart is cheap and boundary-honest: one projection over events the log
already holds, one index (`{userId: 1, at: 1}`), no opening balance to fold,
no per-item state, and the same window instant the task charts use, so
nothing is double-counted or lost at the edge. An item captured and then
organised the same afternoon still counts in the week it arrived, which is
what "captured per week" has to mean.

The costs are real. Statistics owns four collections rather than three
(`adr/0037` said three), and the module has two record shapes a reader must
choose between. The screen no longer says anything about the Inbox *filling
up* — a capture that rots for six months is invisible once its week scrolls
out of the window; if that turns out to be the number the maintainer wanted
all along, the removal records have to be projected again and the backlog
machinery rebuilt from the log. The first week of a window is partial, so the
same calendar week can show a smaller number at the 30-day range than at the
365-day range; the caption says the bar is a week, not that it is a whole
week. And `StatisticsOverview`'s field changing name invalidates the client's
cached payload, so the `localStorage` key's shape version is bumped — an
overview cached under an older key is ignored and re-fetched rather than
rendering a missing series as null.
