---
title: Backfill TodoTask.CreatedAt from TaskCreated, and bump seq so delta sync delivers it
tags: [persistence, sync, analytics]
date: 2026-09-16
status: Active (`BurndownRule` and the client-side burndown chart described here are deleted and superseded by [0037](0037-statistics-owns-denormalised-records.md)'s outstanding-open chart; the `CreatedAt` backfill and the `SyncReader` marker fix stand unaffected)
---

# ADR-0019: Backfill TodoTask.CreatedAt from TaskCreated, and bump seq so delta sync delivers it

## Context

The History screen's burndown chart (open-task count over time) needs to know
when a task was created — `TodoTask` had no `CreatedAt` field until this
branch. Adding the field is free for tasks created from here on (it is set
from `TaskCreated`'s event timestamp, no wire or event-shape change, sync
already replicates it), but tasks created before this field existed have
nothing to read it from on the document itself. The value is not lost,
though: it is recoverable, because AD-2 keeps events as the log beside the
aggregate document — each such task's own `TaskCreated` event still carries
its original timestamp.

## Decision

We will run a one-time boot-time backfill, `MongoBackfill.EnsureCreatedAtAsync`
(`src/shared/PSPad.Infrastructure/Mongo/MongoBackfill.cs`), alongside
`MongoIndexes.EnsureAsync` in `Program.cs`. For every `todotasks` document
missing `createdAt`, it looks up that task's earliest `TaskCreated` event by
`aggregateId` and sets `createdAt` from the event's `at` timestamp. A
document with no matching `TaskCreated` event (an orphan) is left untouched
and consumes no seq. A document that already has `createdAt` is skipped.

The backfill also bumps that document's `seq` — via a real
`SequenceSource.NextAsync` call inside a Mongo session, in the same
`UpdateOneAsync` as the `createdAt` write. This was fixed during this
branch's own review: without it, an already-synced client's delta-sync query
(`Filter.Gt(document.Seq, since)`) would never re-select the document, so an
installation that had synced before the backfill ran would never receive the
recovered value.

Bumping a document's `seq` with no corresponding event breaks an invariant
`SyncReader`'s marker computation depended on: every other path that
allocates a `seq` (`MongoUnitOfWork`) pairs it with an event, so the marker
used to be derived purely from the events actually returned. This backfill
is a document-only `seq` bump with no event, so `SyncReader.ReadAsync`
(`src/PSPad.Api/Sync/SyncReader.cs`) was changed, as a general robustness fix
and not a special case for this backfill, to compute the marker as
`Math.Max(highestEventSeq, highestDocumentSeq)`, where `highestDocumentSeq`
is the highest `seq` among all rows actually returned across every
collection in the response. A document-only `seq` bump — this backfill, or
any future case — now still advances the client's marker correctly.

`BurndownRule.Build(tasks, today, days, zone)`
(`src/modules/PSPad.Module.Tasks/Analytics/BurndownRule.cs`) is pure,
WASM-safe domain logic living in `PSPad.Module.Tasks` (AD-4 compliant). It
computes, per day in the requested window and bucketed in the user's time
zone, the count of tasks open that day and the count of tasks completed that
day.

Two further decisions belong to this same rule, from its original design,
recorded here since this ADR is where the whole feature's reasoning lives:

- **Recurring task templates never count toward the open-count line.** A
  recurring template never closes, so counting it as permanently open would
  add a flat, ever-growing offset that swamps the real signal. Its individual
  occurrences still count toward the completion bars on the days they were
  actually done (`CompletedDays.Contains(day)`).
- **State is truth, not an event-replayed history, so the chart is a live
  recomputation, not a frozen record.** Deleting a task today retroactively
  changes what the chart shows for past days: a task open on day N that gets
  deleted today no longer appears as open on day N the next time the chart
  recomputes, even though it genuinely was open that day. This is an accepted
  consequence of AD-2 applied to this feature, not a bug — documented here
  precisely so it is not "fixed" later without recognizing it as intended.

`HistoryPage.razor` (`src/PSPad.App/Pages/HistoryPage.razor`) renders the two
series as two separate `MudChart<double>` instances — a `Line` chart for the
open-count series and a `Bar` chart for completions, sharing one label axis —
rather than one mixed chart, because MudBlazor 9.9.0's `ChartType` enum has
no combined line+bar type. A UI implementation detail, not the focus of this
ADR.

## Considered alternatives

- **Don't bump `seq` on backfill** — accept that already-synced installations
  never receive the recovered `createdAt` except incidentally, via some
  unrelated future edit to the same task. Rejected: it defeats the
  backfill's purpose for exactly the installations it exists to help — those
  that have been running, and syncing, since before this field existed.
- **A server-side `/api/analytics` endpoint** computing the burndown series
  instead of client-side computation. Rejected per the spec: offline would
  show a blank chart, violating the offline-first requirement (AGENTS.md §4).
- **Separate server and client burndown engines.** Rejected as needless
  duplication of AD-3's "one model of behavior, not two" — `BurndownRule`
  is written once, in the Tasks module, and runs the same on both sides.

## Consequences

Existing installations' burndown charts become accurate without requiring a
user to touch every old task by hand. The `SyncReader` marker fix is a
general robustness improvement — any future document-only `seq` bump is
covered, not just this one — rather than a one-off hack scoped to this
backfill.

Negative: the retroactive-deletion behavior described above is a real,
easy-to-miss surprise for anyone reading the chart's history literally as
"what was true on that day" rather than "what the current state implies was
true on that day." The backfill itself runs a full, unindexed `todotasks`
collection scan (`Filter.Exists("createdAt", false)`) on every boot — a
known, accepted minor cost at this app's current scale, not something this
decision proposes fixing.
