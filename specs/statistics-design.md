# Statistics — design

Supersedes the History module. Drives GitHub issue #21.

A design narrative, not a rulebook, and therefore temporary. This repo keeps
two standing rulebooks — `specs/backend-spec.md` and `specs/ui-spec.md` — and
retires narratives into git history once their work is built, as
`slice-design.md` and the UI redesign specs already were. When this is
implemented, its standing rules fold into those two files and this one is
deleted; the reasoning and the rejected alternatives survive in ADRs 0034–0036.

Where this and an ADR disagree, the ADR wins. Where this and the code
disagree, say so rather than silently following either.

---

## 1. Why

The History module reads the `events` collection directly and maps each
`StoredEvent.Type` through a string dictionary. It has no model of its own, so:

- History is a view over another context's internal events. Renaming a Tasks
  event silently changes what the History screen says.
- The burndown chart is not history at all. `HistoryPage` computes it in the
  browser from the IndexedDB replica through `BurndownRule`, which holds
  current state only — so deleting a task retroactively flattens a week you
  actually worked.
- Nothing subscribes to anything. Events are written and then only ever read
  back as rows; no other module can react to them.

Statistics replaces it with its own bounded context: its own records, its own
collections, fed by domain events published after commit.

## 2. Decisions

| # | Decision |
|---|----------|
| S1 | `PSPad.Module.History` becomes `PSPad.Module.Statistics`. One module owns both the record feed and the charts. |
| S2 | Domain events are dispatched **asynchronously after the transaction commits**. No strong consistency between a command and its statistics. |
| S3 | A background pump drains dispatched events. On startup it first replays the log from a stored marker, then drains live events. |
| S4 | Statistics keeps **denormalized, read-only records**. Deleting a task never alters them. |
| S5 | Domain events carry the fields consumers need, so a record is built from the event payload alone. |
| S6 | Records store facts; anything time-zone dependent is derived at query time. |
| S7 | The screen is server-rendered data over plain REST — no replica, no outbox. The client caches the last payload. |
| S8 | Cross-module reads go through a port Statistics declares and Api implements. |

## 3. Module layout

```
src/modules/PSPad.Module.Statistics/
  Records/          StatisticsRecord, RecordKind
  Records/Handlers/ one handler per consumed event
  Labels/           StatisticsLabel projection (area, list, goal names)
  Charts/           derivations: completions, opened, outstanding, by goal, heatmap
  Ports/            ITaskSnapshotSource, TaskSnapshot, TaskStatus
  StatisticsReader  feed paging + current-status join
  StatisticsOverviewReader
```

References: `Statistics → PSPad.Abstractions + PSPad.Module.Tasks` (event types
only). It never references `PSPad.Infrastructure`; a guard test enforces that,
alongside the existing Tasks purity guard.

## 4. Event flow

New in `PSPad.Abstractions`:

```csharp
public sealed record DomainEventEnvelope(long Seq, DomainEvent Event);

public interface IDomainEventDispatcher
{
    Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct);
}

public interface IDomainEventHandler
{
    Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct);
}
```

The handler is deliberately not generic. Statistics has two of them — records and
labels — and each switches on the event type exactly as `Aggregate.When` already
does. A generic `IDomainEventHandler<TEvent>` would buy nothing but reflection to
resolve the closed type at dispatch.

`MongoUnitOfWork.CommitAsync` already assigns every event its `seq` inside the
transaction. After `CommitTransactionAsync` returns it publishes those
envelopes. A dispatcher failure is caught and logged and never turns an accepted
command into a failed one — the write did succeed.

The in-process dispatcher writes to a bounded `Channel<DomainEventEnvelope>`.
A single `DomainEventPump : BackgroundService` drains it, opening a DI scope per
batch and resolving every registered `IDomainEventHandler`.

**Startup replay.** Before draining, the pump reads Statistics' stored
`lastProcessedSeq` and replays `events` from there to the head through the same
handlers. Replayed seqs are all lower than any live one, so ordering holds and
the marker only moves forward. This backfills an existing database on first
boot and repairs anything a crash lost between commit and handler.

Replay needs to turn a stored JSON payload back into its event type. A
`DomainEventCatalogue` in Statistics maps type name to CLR type, mirroring the
existing `CommandCatalogue`.

**Idempotency.** A record's `_id` is the event's `seq`. Replay and duplicate
dispatch upsert over themselves; duplicates are impossible by construction.

**Future transport.** Replacing the channel-backed dispatcher and the pump's
input with a broker leaves handlers, records and replay untouched.

## 5. Record model

Three collections, all owned by Statistics.

### `statistics_records`

```csharp
public sealed class StatisticsRecord
{
    public long Id { get; init; }                  // the event's seq
    public Guid UserId { get; init; }
    public DateTimeOffset At { get; init; }
    public RecordKind Kind { get; init; }
    public Guid TaskId { get; init; }
    public string TaskName { get; init; } = "";
    public Guid? ListId { get; init; }
    public Guid? GoalId { get; init; }
    public DateOnly? DueOn { get; init; }
    public DateOnly? OccurrenceDay { get; init; }
    public int? CompletionNumber { get; init; }
}

public enum RecordKind
{
    Created, Completed, Reopened, Deleted, Moved, LinkedToGoal, OccurrenceTicked
}
```

Records are never updated or deleted by user action. There are no Statistics
commands and no write endpoints.

`CompletionNumber` is `count(Completed records for this TaskId with a lower
seq) + 1`. Seq-ordered, so replay reproduces it exactly. A value above 1 renders
as *re-checked*, and it is the "how many times was this finished" counter issue
#21 asks for. `OccurrenceTicked` is counted separately — ticking a daily habit
is not re-checking a task.

No stored `Day` and no stored `WasPlanned` flag: both depend on the user's time
zone, and storing them would freeze history in whatever zone was configured at
the time. Deriving them at query time also keeps the pump free of any Identity
dependency.

### `statistics_labels`

`(Id, UserId, Kind: Area | List | Goal, Name, Deleted)`, projected from
`AreaCreated/Renamed/Deleted`, `TaskListCreated/Renamed/MovedToArea/Deleted`,
`GoalCreated/Renamed/Deleted`. A deleted list still has a name in the feed, and
no name lookup ever reaches into Tasks.

### `statistics_state`

`lastProcessedSeq`, one document.

## 5.1 Event enrichment

`TodoTask` knows its own name, list, goal and due date, so its events can carry
them. It does not know list or area *names* — ADR-0008 keeps cross-aggregate
links as ids — which is what `statistics_labels` exists for.

| Event | Current shape | Adds |
|---|---|---|
| `TaskCompleted` | `(AggregateId, UserId, At)` | `Name`, `ListId`, `GoalId`, `DueOn` |
| `TaskReopened` | `(AggregateId, UserId, At)` | `Name`, `ListId` |
| `TaskDeleted` | `(AggregateId, UserId, At)` | `Name` |
| `OccurrenceCompleted` | `(…, Day)` | `Name`, `ListId`, `GoalId` |
| `TaskMovedToList` | `(…, ListId)` | `Name` |
| `TaskLinkedToGoal` | `(…, GoalId)` | `Name` |

`TaskCreated` already carries `ListId` and `Name`.

Events stored before this change deserialize with the new fields empty. Replay
fills `TaskName` best-effort through `ITaskSnapshotSource` (§6); if the task is gone
the record reads *(deleted task)*. A task renamed since will show its current
name — recoverable only for pre-change history, and accepted.

## 6. Cross-module boundary

Current status ("does this task still exist, is it open") needs Tasks' live
documents. Statistics declares the port and its own DTO; Api implements it:

```csharp
public enum TaskStatus { Open, Done, Gone }

public sealed record TaskSnapshot(TaskStatus Status, string Name);

public interface ITaskSnapshotSource
{
    Task<IReadOnlyDictionary<Guid, TaskSnapshot>> CurrentAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct);
}
```

The name is on the snapshot for one reason only: replay of pre-enrichment
events (§5.1) has no other source for it. The feed itself always renders the
record's own `TaskName`.

One batched lookup per feed page, never per row. Statistics never touches
`IDocumentStore<TodoTask>`.

## 7. Charts

Every chart derives from `statistics_records` alone, so a deleted task cannot
rewrite the past. One range selector — 30 / 90 / 365 days — drives all of them.
Days are bucketed at query time in the user's time zone.

| Chart | Question | Derivation |
|---|---|---|
| Completions per day (stacked bar) | how much of what I did was on the plan | `Completed` + `OccurrenceTicked` per day, split planned / unplanned |
| Tasks opened per day (line) | am I taking on more than I finish | `Created` per day |
| Open tasks outstanding (line) | the burndown, truthfully | running `Created − Completed − Deleted + Reopened` |
| Where the work went (horizontal bars) | is my effort going where I said | `Completed` grouped by `GoalId`, ranked, with an explicit *no goal* bar |
| Consistency heatmap | am I showing up | one cell per day shaded by completion count |

**Planned** means the record has a `DueOn` on or before the completion day, or
is an occurrence tick. **Unplanned** means no due date, or one still in the
future.

The *no goal* bar is the point of the goal chart: on a real GTD board it is
usually the largest, and seeing that is the insight.

The outstanding-open line replaces `BurndownRule`, which is deleted along with
its tests and the `Analytics` folder in the Tasks module.

The heatmap has no MudBlazor equivalent and is a custom CSS grid. Habits
(subsystem 4) reuses it for streaks.

**Tiles** above the charts: done today, opened today, done this week, net change
over the range (opened − closed, signed). Same aggregation the charts already
compute.

## 8. HTTP surface

```
GET /statistics/overview?days=30
    → tiles, the four series, byGoal[], heatmap[]

GET /statistics/records?before=<seq>&limit=50
    → paged feed with list and goal names, CompletionNumber, CurrentStatus
```

Both `[Authorize]`, both scoped through `ICurrentUser`. Wire shapes live in
`PSPad.Contracts`.

## 9. Client

`StatisticsPage.razor` at `/statistics`, talking plain REST through
`PSPadApiClient`. No replica, no outbox, no command pipeline.

The last successful `overview` payload is cached in localStorage keyed by
range. The page renders the cache immediately when present, refreshes behind
it, and shows *updated <relative>* plus an offline banner when a request fails.

That cache is user data. ADR-0018 and ADR-0025 purge the replica and outbox on
a signed-in/owner mismatch; the statistics cache purges on the same path, or
user B sees user A's numbers before the first fetch lands.

`/history` redirects to `/statistics` so existing PWA bookmarks survive. The
sidebar row is relabelled.

**Layout** — `MudGrid`: tiles `sm=3 xs=6`, charts `md=6 xs=12`, heatmap and feed
full width.

## 10. Testing

**Unit** (`PSPad.Module.Statistics.Tests`): handlers produce the expected
records; `CompletionNumber` ordering under replay; planned/unplanned
classification across time zones, including a DST boundary; outstanding-open
derivation through reopen and delete; heatmap bucketing; label projection
through rename and delete.

**Unit** (`PSPad.App.Tests`, bUnit): cached payload renders before the fetch
resolves; offline banner; feed grouping and the re-check marker.

**Integration** (`PSPad.Api.Tests`): dispatch happens after commit and never
fails an accepted command; the pump writes records; replay from seq 0 backfills
an existing database; double dispatch yields exactly one record; endpoints
refuse another user's data; `CurrentStatus` reports `Gone` for a deleted task.

**Guards**: Statistics references no infrastructure; Tasks still compiles to
WASM after enrichment.

**Indexes** added to `MongoIndexes.EnsureAsync`: `(UserId, Id desc)` for the
feed, `(UserId, Kind, At)` for the charts, `(UserId, TaskId, Kind)` for the
completion count.

## 11. ADRs

Written with the change, not after.

- **0034** — domain events dispatched asynchronously after commit, replayed
  from a marker on startup.
- **0035** — Statistics owns denormalized read-only records projected from the
  log, and domain events carry what consumers need. Must explicitly reconcile
  with ADR-0011's *"never write a parallel audit table"*: a read-side
  projection is not an audit table, and the write side still writes exactly one
  log — but that sentence reads as a contradiction without the explanation.
- **0036** — History renamed to Statistics; route and sidebar row. Amends
  ADR-0020.

AGENTS.md §2, §3, §5 and §8 are updated in the same work: the subsystem row,
AD-1's module list, the burndown references, and the current step.

## 12. Docs

`docs/src/pages/features.astro` and the landing page gain the statistics
screen. `install.astro` is untouched — no new service, port or environment
variable.

## 13. Out of scope

- Retention or archival of records. Unbounded, as events are today.
- Effort-by-area chart. Same shape as the goal chart; left out until the goal
  chart proves itself.
- A ranked "tasks you keep reopening" list. The counter ships on the record and
  renders in the feed; the standalone list can follow.
- Any client-side projection. The client never handles domain events.
