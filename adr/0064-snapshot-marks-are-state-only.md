---
title: Anonymous snapshot marks change state only, under a daily budget per list
tags: [architecture, persistence, event-sourcing, sharing, security]
date: 2026-10-12
status: Active
---

# ADR-0064: Anonymous snapshot marks change state only, under a daily budget per list

> Status: Active (amends [ADR-0011](0011-aggregate-documents-are-truth.md) and [ADR-0056](0056-public-snapshots-are-frozen-copies.md))

## Context

Anyone holding a snapshot link may tick or untick its entries (ADR-0056).
Each change ran `MarkTaskFromSnapshot` or `MarkReferenceItemFromSnapshot`,
and ADR-0011 has every command write its aggregate, its events and its
command id together. A visitor flipping one checkbox in a loop therefore
appended a permanent `events` row and a 30-day `processed_commands` row under
the owner's account on every flip (#132). The only guard was the per-IP
limiter, which a forged `X-Forwarded-For` defeats when the API trusts
forwarded headers; a snapshot can live a year.

Nothing reads those events: documents are truth and never replayed, sync no
longer ships the log (ADR-0061), and statistics ignores marks. The command id
guards nothing either — the server mints a fresh one per flip, so no replay
can ever match it.

## Decision

1. `TaskSnapshotMarkSet` and `ReferenceItemSnapshotMarkSet` implement
   `IUnloggedEvent`. `Decide` and `When` treat them like any event, but the
   unit of work writes no `events` row for them and does not dispatch them.
   It still takes a sequence number and stamps the aggregate's `seq`, so
   delta sync delivers the changed document. A commit whose events are all
   unlogged records no `processed_commands` row.
2. Marks reaching a list are capped at
   `SnapshotMarking.DailyMarksPerList` (100) real changes per UTC day, across
   all of that list's snapshots and all visitors. Sharing takes one unit from
   `IMarkBudget` before saving a change; with none left the mark endpoint
   answers `429` and nothing is written. Unchanged marks cost nothing. The
   counter lives in `snapshot_mark_budgets`, one row per list and day,
   removed by TTL two days later.

## Considered alternatives

- **Keep the events, rate-limit per token in memory** — rejected: the log
  still grows by up to the limit forever, the counter resets on restart, and
  a list with several snapshots multiplies it.
- **Coalesce toggles that revert within N seconds** — rejected: it needs a
  delayed write or a background job, and still logs every toggle that
  outlives the window.
- **A per-snapshot budget stored on the snapshot document** — rejected: the
  owner can publish several links to one list; the list is what the owner
  reads and what the limit should protect.

## Consequences

- Storage per mark is bounded: the task or item document and the snapshot
  document are overwritten, and at most 100 changes a day reach any list.
- The owner keeps only the current marks with their `markedAt`; there is no
  record of earlier ticks and unticks. Mark events written before this
  change stay in the log.
- `seq` now has gaps where an unlogged event took a number without a log
  row. Sync and projection replay read by `seq > marker` and never assume
  contiguity, so nothing depends on it.
- A busy shared list (a group shopping list) can hit 100 in a day; visitors
  then see "Too many ticks on this list" until the next UTC day. Raising the
  constant is a one-line change.
- `IUnloggedEvent` is available to any future event that must change state
  without history. Using it gives up that history, so each use needs its own
  decision record.
