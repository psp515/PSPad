---
title: Statistics owns denormalized read-only records projected from the log, and domain events carry what consumers need
tags: [architecture, persistence, analytics]
date: 2026-09-24
status: Active
---

# ADR-0037: Statistics owns denormalized read-only records projected from the log, and domain events carry what consumers need

## Context

The History module had no model of its own — it read `events` directly and
mapped `StoredEvent.Type` through a string dictionary at render time. That
made it a view over another module's internal representation: renaming a
Tasks event silently changed what the screen said, and nothing about it
could be queried or charted without re-reading and re-interpreting the raw
log on every request. Separately, `TodoTask` is mutable current state (AD-2)
— a task's own document does not remember what it was named when it was
completed, or that a now-deleted task was ever completed at all. A screen
that wants to show "what happened" truthfully cannot read that from current
state.

## Decision

`PSPad.Module.Statistics` (ADR-0038 renames it from `PSPad.Module.History`)
owns three MongoDB collections of its own, populated only by
`IDomainEventHandler`s reacting to the dispatch in ADR-0036 — never written
by a command, never exposed to a write endpoint:

- `statistics_records` — one `StatisticsRecord` per relevant domain event,
  `_id` = the event's `seq`. Immutable once written; never updated or
  deleted by user action.
- `statistics_labels` — a name projection (`Area`/`List`/`Goal` → current
  name, `Deleted` flag) fed by the corresponding created/renamed/deleted
  events, so the feed can show a list's or goal's name without Statistics
  ever reading `Tasks`' own collections.
- `statistics_state` — one document holding `lastProcessedSeq`, the replay
  marker ADR-0036 describes.

**This does not reopen ADR-0011's "never write a parallel audit table."**
ADR-0011 forbids a second table written *alongside* the aggregate write, in
the same path, as an alternate record of "what happened" that could drift
from the event log — the thing that made Marten's event store unnecessary
to replace. `statistics_records` is not that. The write side still writes
exactly one log: `events`, inside `MongoUnitOfWork.CommitAsync`'s single
transaction, unchanged by this decision. `statistics_records` is built
*after the fact*, by a different module, entirely from that one log — it is
a read-side projection, rebuildable from `events` alone by replaying from
seq 0, not a second source of truth anything else depends on being correct.
If every row in `statistics_records` vanished tonight, the next boot's
replay would reconstruct it exactly, because nothing but `events` fed it in
the first place. `TodoTask` documents remain the only source of truth for
current task state; `statistics_records` is a projection of history, never
consulted to decide anything.

**Domain events carry the fields Statistics needs, so a record is built
from the event payload alone.** Six event records —
`TaskCompleted`, `TaskReopened`, `TaskDeleted`, `TaskMovedToList`,
`TaskLinkedToGoal`, `OccurrenceCompleted` — grew fields (`Name`, and
`ListId`/`GoalId`/`DueOn` where relevant) they did not carry before, mirroring
what `TaskCreated` already had. This is what keeps history truthful: a task
renamed or deleted after the fact still shows the name it had *at the time
of the event*, not its current name or a lookup that fails once the task is
gone. Events stored before this change deserialize with those fields absent
(default/empty). The reader (`StatisticsReader`) falls back to the live task
snapshot through `ITaskSnapshotSource` for a record with an empty name, or
renders `(deleted task)` when the task no longer exists. This is
best-effort and openly so — a task renamed since a pre-change event was
recorded shows its *current* name for that old record, not the name it had
then, because that information was never captured. Only events recorded
from this change forward are exempt from that limitation.

**Occurrences are a toggle, not two event types.** `OccurrenceCompleted`
carries `bool Completed`, so ticking a recurring day and un-ticking it later
emit the same event type with the flag flipped. Both become
`StatisticsRecord`s (`OccurrenceTicked` / `OccurrenceUnticked`) — there is
no update-in-place, because records are immutable and keyed by `seq`, not
by `(TaskId, OccurrenceDay)`. A chart that needs "is this occurrence
currently ticked" resolves each `(TaskId, OccurrenceDay)` pair to the record
with the highest `Id` (`seq`) among its tick/untick records
(`StatisticsCharts.FinalOccurrenceTicks`). Dropping the untick event instead
of recording it would leave the earlier tick's record standing forever, and
every chart derived from it would over-count that day permanently.

**Outstanding-open count is derived from per-task state, not a signed sum.**
A naive `Created + Reopened − Completed − Deleted` decrements twice for a
task that is completed and then deleted — the running total can go negative
for reasons that have nothing to do with reality. `StatisticsCharts.Outstanding`
instead tracks a set of currently-open task ids: a task is open if and only
if its *latest* lifecycle record (`Created`, `Reopened`, `Completed`, or
`Deleted`, ordered as below) is `Created` or `Reopened`. `Created` and
`Reopened` add the task id to the open set; `Completed` and `Deleted` remove
it — idempotent regardless of how many times a task cycles through
complete/reopen/delete.

**Records are ordered by `(day, seq)`, not `seq` alone, wherever chart
derivation cares about chronology.** PSPad is offline-first: a command
authored on a disconnected device can commit days after it was created, so
an old wall-clock timestamp can carry a higher `seq` than a same-day command
issued elsewhere. Bucketing and applying lifecycle transitions in pure
`seq` order would apply a late-arriving offline completion to the wrong
day's running total. `StatisticsCharts.Outstanding` orders its lifecycle
records by the record's bucketed day first, `Id` (`seq`) only as the
tiebreaker within a day.

## Considered alternatives

- **Query `events` directly per chart, as History did, instead of a
  projection.** Rejected: every chart re-parsing and re-interpreting the raw
  log at request time is slower as the log grows, duplicates the
  interpretation logic across every reader, and gives Statistics no place
  to store derived facts (`CompletionNumber`, resolved names) that the raw
  event alone cannot answer without a second live lookup on every read.
- **Compute current-status-dependent history (e.g. "is this task still
  open") by joining to `TodoTask` on every read, storing nothing.**
  Rejected for the feed and charts alike: it makes "what happened" a
  function of "what is true right now," which is exactly the bug this
  decision exists to fix — a deleted task's past completions would vanish
  from the chart the moment the task is deleted, not stay as history.
  `ITaskSnapshotSource` is still used, but only for the narrow pre-enrichment
  name fallback (§ above), never to decide a chart's shape.

## Consequences

Charts and the feed read `statistics_records` alone — fast, and immune to a
task's later deletion or rename changing what already happened. The cost:
Statistics keeps a copy of facts (`TaskName`, `ListId`, `GoalId`, `DueOn`)
that also live, in current form, on `TodoTask` — two representations of
"what was this task called," one historical and one current, and a reader
has to know which one it wants. Pre-enrichment events are a known, permanent
gap: their records can never recover the name a task had *at that time*,
only whatever ITaskSnapshotSource can offer *now*, which decays as tasks are
renamed or deleted further from that date. `statistics_state`'s marker being
global rather than per-consumer (see ADR-0036) also means Statistics cannot
today distinguish "the label projection is caught up" from "the record
projection is caught up" — both ride the same marker, which is fine while
there are only two handlers sharing one replay pass, but would need
splitting if a future consumer needed a different replay cadence.
