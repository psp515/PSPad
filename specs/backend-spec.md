# Backend Spec

Standing rules for the command pipeline, storage, sync, identity and HTTP
surface — everything below the UI. This is a rulebook, not a history: it
states what the system does now. For *why*, the superseded design
narratives (`slice-design.md`, `offline-first-session-design.md`,
`statistics-design.md`) are in git history (`git log -- specs/`); the ADRs
they produced stay in `adr/` and remain the decision record.

Where this spec and an ADR disagree, the ADR wins. Where this spec and the
code disagree, say so rather than silently following either. AGENTS.md §5
(AD-1…AD-10) is the terse architectural summary this spec expands on —
read that first for the "why one modular monolith", this for the mechanics.

---

## 1. Project layout & references

```
PSPad.slnx
src/
  PSPad.Api/                   Minimal API, endpoints, DI composition, Dockerfile
                               (Snapshots/ holds the Sharing module's Mongo and
                               command adapters, namespaced `PSPad.Api.Snapshots`
                               — not `.Sharing`, to avoid clashing with a test helper)
  PSPad.App/                   Blazor WASM PWA, MudBlazor, IndexedDB replica + outbox, Dockerfile (nginx)
  shared/
    PSPad.Abstractions/        ICommandHandler<T>, IDocumentStore<T>, IUnitOfWork, IClock, Aggregate
    PSPad.Contracts/            wire shapes: command envelope, sync DTOs, statistics DTOs
    PSPad.Infrastructure/       Mongo client, connection string, generic repository, Keycloak/JWT, DI registration
  modules/
    PSPad.Module.Tasks/         areas, lists, Inbox, tasks, steps, goals, recurrence, Today, reference items — pure, WASM-safe
    PSPad.Module.Statistics/    queries over the event log
    PSPad.Module.Identity/      User, time zone, first-sign-in provisioning
    PSPad.Module.Presentation/  per-user views of shared data (AreaView: list order) — pure, WASM-safe
    PSPad.Module.Sharing/       public snapshots — frozen list copies, server-side only (adr/0056)
test/
  PSPad.Module.Tasks.Tests/       unit only
  PSPad.Module.Statistics.Tests/  unit only
  PSPad.Module.Identity.Tests/    unit only
  PSPad.Module.Presentation.Tests/ unit only
  PSPad.Module.Sharing.Tests/     unit only
  PSPad.Api.Tests/                integration, Testcontainers MongoDB
  PSPad.App.Tests/                unit + bUnit
  PSPad.TestInfrastructure/       Mongo fixture, category attributes, architecture guards
docker/                         compose files, Keycloak realm, nginx config
```

References run one way only:

| Project | May reference |
|---|---|
| `PSPad.Abstractions` | nothing |
| `PSPad.Contracts` | `PSPad.Abstractions` |
| `PSPad.Module.Tasks` | `PSPad.Abstractions` — and nothing else. This is the purity rule (AD-4) |
| `PSPad.Module.Statistics` | `PSPad.Abstractions`, `PSPad.Contracts`, `PSPad.Module.Tasks` (event types only, for its handlers' `switch` patterns) |
| `PSPad.Module.Identity` | `PSPad.Abstractions`, `PSPad.Contracts` |
| `PSPad.Module.Presentation` | `PSPad.Abstractions` — and nothing else; no module references it back (`adr/0051`) |
| `PSPad.Module.Sharing` | `PSPad.Abstractions`, `PSPad.Module.Tasks` (aggregate shapes only, to build a snapshot — `adr/0056`) |
| `PSPad.Infrastructure` | `PSPad.Abstractions`, `PSPad.Contracts` — never a module |
| `PSPad.Api` | everything |
| `PSPad.App` | `PSPad.Module.Tasks`, `PSPad.Module.Presentation`, `PSPad.Abstractions`, `PSPad.Contracts` — never `PSPad.Infrastructure` |

`PSPad.Infrastructure` stores documents generically by `T`, so it never
needs to know a module exists. `PSPad.App` references `PSPad.Module.Tasks`
because the same command handler runs in the browser against the IndexedDB
replica and on the server against MongoDB (AD-3) — the only reason offline
edits and server state agree.

Enforced by architecture guard tests, not just this document: no
`MongoDB.*`/`Microsoft.AspNetCore.*`/`System.Net.Http` inside
`PSPad.Module.Tasks` or `PSPad.Abstractions`; no `PSPad.Infrastructure`/
`MongoDB.*`/`Microsoft.AspNetCore.*` inside `PSPad.Module.Statistics` or
`PSPad.Module.Sharing`; no module reference inside `PSPad.Infrastructure` or
`PSPad.Module.Presentation`; no `PSPad.Module.Presentation` or
`PSPad.Module.Sharing` reference inside `PSPad.Module.Tasks`; no
`PSPad.Module.Statistics`/`PSPad.Module.Presentation` reference inside
`PSPad.Module.Sharing`.

Two module-to-module edges exist, and only these two: `Statistics` →
`Tasks` and `Sharing` → `Tasks`, both one way and both for the same reason —
each pattern-matches on Tasks' own event or aggregate types, so a rename
breaks the build rather than a mismatch surfacing at render time
(`adr/0037`, `adr/0056`). `ArchitectureTests` guards both directions failing
the build if `Tasks` ever references either back.

Commands are discovered by reflection over `CommandModules.Names`
(`PSPad.Contracts`) — `PSPad.Module.Tasks` and `PSPad.Module.Presentation` —
by `CommandCatalogue` and by both hosts' handler registration. A new
command-carrying module is added there, once.

What each module owns, must never absorb, and where it grows next is in
`specs/modules-spec.md`.

`PSPad.App` never writes to standard error. Blazor WebAssembly treats any
stderr output as a crash and raises its "An unhandled error has occurred"
banner, so a handled failure (server unreachable, IndexedDB unreadable) logs
through `ILogger` at `Warning` instead. A guard test scans the assembly's IL
for `Console.Error`.

---

## 2. Command pipeline

One shape, both sides of the wire:

```csharp
public interface IAggregate { Guid Id { get; } Guid UserId { get; } int Version { get; } }
public interface ICommand { Guid CommandId { get; } Guid UserId { get; } }
public interface IServerOnlyCommand : ICommand;
public abstract record DomainEvent(Guid AggregateId, Guid UserId, DateTimeOffset At)
{
    public Guid? ActorId { get; init; }
    public Guid Actor => ActorId ?? UserId;
}
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken ct);
}
public sealed record CommandResult(bool Accepted, string? Rejection = null)
{
    public static CommandResult Ok() => new(true);
    public static CommandResult Rejected(string reason) => new(false, reason);
}
```

**Owner and actor** (`adr/0054`). `UserId` on every event is the
**aggregate's owner**; `ActorId` is who actually issued the command, set
only when it differs. `Decide`/`When` never stamp it — `MongoUnitOfWork
.CommitAsync` compares each staged event's `UserId` against the
authenticated caller on the way into the transaction and sets `ActorId`
when they differ, so the Tasks module stays ignorant of sharing entirely.
A creation event (`TaskCreated`, `ReferenceItemCreated`) takes its owner
from the target list, so a member's `CreateTask` produces a task owned by
the list's owner with the member as `ActorId`.

**Server-only commands.** `IServerOnlyCommand` is a marker with no members;
`CommandDispatcher.DispatchAsync` refuses one with
`Unrecoverable: true` before it reaches a handler — `/api/commands` is a
client-facing surface only. A server-only command still runs through
`CommandDispatcher.RunAsync`, called directly from server code that owns
its own trust decision instead of trusting the caller (`JoinTaskList` is
marked this way today; the HTTP entry point that calls it is a later
plan's work).

**List access** (`adr/0054`). `ListAccess.To(list, actorId)` returns an
`OwnerId`/`ActorId` pair for the owner or any member of `list`, and rejects
everyone else; `TaskList.Require` (owner only) stays for list-level
commands. Every content handler — tasks, steps, recurrence, descriptions,
stars, priorities, due dates, completion, occurrences, reference items and
their fields, including `LinkTaskToGoal` — loads the parent `TaskList` to
resolve `ListAccess` (`ListAccessLoading.AccessAsync`), even one that
previously loaded only its own aggregate. `LinkTaskToGoal` reaches
`TodoTask.Decide` the same way as any content command; the aggregate itself
then rejects a non-owner `ListAccess.ActorId` (goals are the owner's), so
the owner-only gate sits in the aggregate's rule, not in which loader the
handler calls.

Aggregates keep rules pure and testable with no store: `Decide(command)`
returns the events a command produces, or throws `DomainRejectedException`;
`Apply(event)` folds one event into state.

A handler does exactly four things — load, decide, apply, stage — and
never names a database type:

```csharp
public interface IDocumentStore<T> where T : Aggregate
{
    Task<T?> LoadAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct);
}
public interface IUnitOfWork
{
    void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events);
    Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct);
}
```

Writing is separate from reading because one command can touch two
aggregates (organising an inbox item creates a task and empties the item),
and both must land together or not at all. On the server, `CommitAsync`
persists every staged aggregate, appends their events, bumps the sync
sequence and records the command id **in one MongoDB transaction**. In the
browser, the same call writes the IndexedDB replica and appends to the
outbox.

`Aggregate` carries one settable `long Seq`, stamped with the sequence of
the last event that touched it — data about the write, never the domain,
and no `Decide` may read it.

**Idempotency.** Every command carries a client-generated `CommandId`. The
server records processed ids in `processed_commands` inside the same
transaction as the write. A replayed command (the outbox shipping twice
after a flaky reconnect) finds its id present and returns the earlier
result without writing.

---

## 3. Storage

**MongoDB 8, single-node replica set `rs0`.** Not optional — multi-document
transactions require one. Dev, prod and tests all run the same shape.

| Collection | Holds | Notable fields |
|---|---|---|
| `users` | one per person | `timeZone` (IANA), `provisionedAt` |
| `areas` | user-defined areas | `name`, `position` |
| `tasklists` | task lists, each inside one area | `areaId`, `name`, `createdAt`, `kind` (`Tasks` or `Reference`, fixed at creation), `inviteToken` (`adr/0054`, null = not shared), `inviteCode`, `inviteExpiresAt` (`adr/0057`; 30 minutes from the share), `wrongCodes`, `ownerName`, `_members[]` (`userId`, `displayName`, `joinedAt`; the leading underscore keeps the BSON field name stable, like `_steps`). Documents written before `adr/0051` still carry a `position` nobody reads |
| `inboxes` | one per user | `items[]` |
| `todotasks` | tasks with steps inline | `listId`, `previousListId` (the list it last left, null until moved), `dueOn`, `goalId`, `priority`, `starred`, `steps[]`, `recurrence`, `leadTime`, `completedDays[]`, `createdAt`, `description` (Markdown), `snapshotMarks[]` (`snapshotId`, `stepId?`, `markedAt` — `adr/0056`) |
| `goals` | global goals | `name`, `achieved`, `notAchieved`, `dueOn` |
| `referenceitems` | items in a `Reference` list | `listId`, `previousListId`, `name`, `description` (Markdown), `starred`, `position`, `fields[]` (`label`, `value`, `display?`, `position`), `snapshotMarks[]` (`snapshotId`, `markedAt` — `adr/0056`) |
| `areaviews` | one per (user, area): that user's order of the area's lists | `_id` = `AreaView.IdFor(userId, areaId)`, `areaId`, `order[]` (list ids) |
| `listviews` | one per (user, list): that user's placement of a shared list | `_id` = `ListView.IdFor(userId, listId)`, `listId`, `areaId?` (null = "Shared with me") |
| `events` | the domain event log and the sync feed | `seq`, `userId`, `aggregateType`, `aggregateId`, `type`, `payload`, `at` |
| `processed_commands` | idempotency keys | `_id` = command id, `at` |
| `counters` | the global sequence | `_id: "events"`, `value` |
| `statistics_records` | the statistics feed and every chart | `_id` = `"{seq}:{userId}"` (`adr/0055`), `seq`, `role` (`Owner` \| `Actor`), `userId`, `at`, `kind`, `taskId`, `taskName`, `listId`, `goalId`, `dueOn`, `occurrenceDay`, `completionNumber` |
| `statistics_inbox_records` | the Inbox-captures chart | `_id` = the event's `seq`, `userId`, `at`, `itemId` |
| `statistics_labels` | area, list and goal names for the feed | `_id` = the aggregate's id, `userId`, `kind`, `name`, `deleted` |
| `statistics_state` | the projection's resume marker | `_id: "statistics"`, `lastProcessedSeq`, `projectionVersion` (`adr/0055`) |
| `list_snapshots` | a frozen, owner-published copy of one list (`adr/0056`) | `_id` (GUID), `token` (unique, server-generated, distinct from `_id`), `userId` (owner), `listId`, `kind`, `name`, `createdAt`, `expiresAt`, `tasks[]` or `items[]` — each entry its own `id`/`name`/`done`/`marked`/`markedAt`, steps or fields nested the same way. Content is frozen at publish; only `marked`/`markedAt` change afterwards |
| `snapshot_visits` | a signed-in visitor's own record of opening a snapshot | `_id` = `"{userId}:{snapshotId}"`, `userId`, `snapshotId`, `token`, `name`, `expiresAt`, `visitedAt` |

Every aggregate document carries `_id` (GUID), `userId`, `version`
(optimistic concurrency), `seq` (sequence of the last touching event) and
`deleted` (soft delete, so a removal travels through sync).

**The sequence.** `counters` holds one document. Each write runs
`findOneAndUpdate({_id: "events"}, {$inc: {value: 1}})` inside the
transaction; the returned value stamps both the event and the aggregate's
`seq`. Monotonic, safe as a resume marker — a clock is not.

**Indexes.**

- `events`: `{userId: 1, seq: 1}`, `{userId: 1, at: -1}`, `{seq: 1}` — the last
  one serves startup replay, which reads forward across every user ordered by
  `seq` alone and so cannot use either compound index
- every aggregate collection: `{userId: 1, seq: 1}` (delta sync)
- `todotasks`: `{userId: 1, listId: 1}`, `{userId: 1, dueOn: 1}`, `{listId: 1, seq: 1}` and `{previousListId: 1, seq: 1}` (a member's delta sync)
- `tasklists`: `{userId: 1, areaId: 1}`, `{_members.userId: 1, seq: 1}` (a
  member's delta sync), unique partial `{inviteToken: 1}` named
  `inviteToken_unique` (`partialFilterExpression: {inviteToken: {$type:
  "string"}}`; join lookup, and no two lists may hold one token — a
  duplicate-key write on commit is rejected as "That invite link is already
  in use."; startup drops the older sparse `inviteToken_1`)
- `referenceitems`: `{userId: 1, listId: 1}`, `{listId: 1, seq: 1}` and `{previousListId: 1, seq: 1}` (a member's delta sync)
- `list_snapshots`: unique `{token: 1}`, TTL on `expiresAt`
  (`expireAfterSeconds: 0`), `{userId: 1, listId: 1}` (the panel's active list)
- `snapshot_visits`: TTL on `expiresAt`, `{userId: 1, visitedAt: -1}` (the
  List snapshots tab). TTL deletion lags the instant, so both collections'
  reads also check `expiresAt > now` themselves rather than trusting the
  sweep to have already run
- `processed_commands`: TTL index on `at`, 30 days
- `statistics_records`: `{userId: 1, seq: -1}` (the feed page, `adr/0055`
  replaces `{userId: 1, _id: -1}` now that `_id` is no longer the bare `seq`),
  `{userId: 1, kind: 1, at: 1}` (the charts' window),
  `{userId: 1, taskId: 1, kind: 1}` (the completion counter)
- `statistics_inbox_records`: `{userId: 1, at: 1}` (the captures window)
- `statistics_labels`: `{userId: 1}`

---

## 4. Domain rules

Authoritative statement lives in AGENTS.md §3 — Today rule and
never-overdue recurrence. This section only adds implementation mechanics
not stated there:

- The Today rule takes the instant and the time zone as explicit
  arguments, so it is deterministic under test — never reads a clock or
  `TimeZoneInfo.Local` internally.
- Recurrence stores a rule plus `completedDays`; Pending/Skipped are
  **derived**, never stored.
- `RecurrenceRule.Interval` (1–99, `EveryNth` rejects the rest) counts from
  `StartsOn`: Daily every Nth day; Weekly on its weekdays in every Nth
  Monday-based week counted from `StartsOn`'s week; MonthlyOnDay on its day,
  clamped to the month's length, in every Nth month counted from
  `StartsOn`'s month. Documents without the field read `Interval = 0`;
  `Every` treats anything below 1 as 1 (`adr/0043`).
- `RecurrenceKind.Yearly` fires on `StartsOn`'s month and day, clamped to the month's length (29 Feb → 28 Feb in non-leap years), in every Nth year counted from `StartsOn`'s year (`adr/0053`).
- A repeating task's `DueOn` is its inclusive end ("Until"). Repeat and due
  date combine in either order. `TodoTask.OccursOn(day)` = rule occurs and
  `day <= DueOn`; Today, My Day's sections, occurrence chips and `CompleteOccurrence`
  all use it, never `RecurrenceRule.OccursOn` alone. A tick after the end is
  rejected as a day the task does not repeat on; an untick of a day already
  in `completedDays` is always accepted, even after the end or interval
  moved past it. `SetTaskRecurrence` rejects an `Interval` outside 1–99,
  letting the legacy 0 through.
- `TodoTask.EndedBy(today)` — repeating and `DueOn < today` — means the
  repeat has ended; the client treats it as completed. Derived, never
  stored: no command, no event, no `CompletedAt`. Moving `DueOn` forward
  reopens it. The end is a cut-off, never a trigger — a repeating task is
  still never overdue.
- `RepeatTally.Of(task, today)` derives the done count (`completedDays`)
  and the streak: done occurrences walking back from today, today's pending
  occurrence not breaking it.
- `TodoTask.LeadTime` (optional; 1–99 days, weeks or months; `SetTaskLeadTime`) is the task's look-ahead for Coming up on My Day; with none it is 7 days. `LeadTime.Shows(day, today)` — `day > today` and `FirstShownFor(day) <= today`. It never moves a task into Today or Overdue (`adr/0053`).
- `TodoTask.Time` (`TaskTime(Start, End?)`; `SetTaskTime`, `TaskTimeSet`) is an
  optional expected start and end, a wall clock in the user's stored zone
  like `DueOn`. `End` must differ from `Start` (`TaskTime.Of` and `Decide`
  reject an equal one with "A task can't end when it starts."); an `End`
  before `Start` runs overnight into the next day (`TaskTime.Overnight`,
  derived, not serialized). The task stays on its start day and Schedule
  orders by `Start` only. `null` clears; no
  event when unchanged; same `ListAccess` grant as `SetTaskLeadTime`. A
  repeating task's time is every occurrence's time. Removing a one-time
  task's due date keeps the time, but an undated task is on no day, so it
  is ignored until a date returns. Documents without the field read `null`.
  `TodayEntry.Time` carries it (additive to `GET /api/today`).
- `TodayRule.Plan(tasks, day, today, zone)` plans the day on screen (`day`;
  `today` is the user's today) as Overdue, Scheduled, AnyTime, Starred,
  ComingUp and Completed. A task's date is its earliest trigger — due date
  or next unchecked step. "Due on `day`" means a one-time open task whose
  trigger is `day`, or a recurring task that `OccursOn(day)` with `day`
  unticked; Scheduled holds those with a `Time` (by start, then name),
  AnyTime those without. By day kind: on a past day only Completed
  (completed that day in the user's zone, or that day's ticked
  occurrence) is filled. On today, Overdue and the due entries are exactly
  `Select`'s, so the Today rule does not move; Starred and ComingUp
  (each one-time task at its trigger, each recurring task at its next
  unticked occurrence, only while `LeadTime.Shows`) fill too. On a future day
  Overdue and ComingUp are empty and lead time is ignored — the user asked
  for that day — while Scheduled, AnyTime, Starred and Completed (ticked occurrences) are
  filled. A
  recurring task is never overdue. Overdue entries keep their `Time`.
- Starred holds open, starred, one-off tasks not overdue or due by the day
  shown (undated first, then by date); they are left out of Coming up. The
  star never enters `Select` (`adr/0035`).
- The rule is tested in three places on purpose: the module (unit), the
  API's Today query (integration), and the client projection (bUnit). A
  change that breaks it should turn three suites red, not one.
- Ordering (lists, steps, inbox items) uses a dense `position` integer.
  Reordering rewrites the affected range rather than using fractional
  keys — ranges are short, and the rewrite touches one document.
- `TodoTask.CreatedAt` is set in `When(TaskCreated)` from the event's `At`.
  It does not back delta sync — that runs off the aggregate's `seq` marker
  alone. It was added to feed the client-side burndown chart (`adr/0019`),
  which this slice deleted; its successor,
  `StatisticsCharts.Outstanding`, is server-side and derives from
  `statistics_records`, so the field has no consumer at all today. It stays
  because it is the only creation timestamp current state carries, and
  `adr/0019`'s backfill already put it on every existing document.
- A goal's status is `InProgress`, `Achieved` or `NotAchieved`, set by
  `SetGoalStatus`. It is derived from two stored flags, `achieved` and
  `notAchieved`, never stored as its own field. Documents written before
  statuses existed carry only `achieved`, so they read correctly with no
  backfill. `AchieveGoal` and `ReopenGoal` still work so that commands
  already queued in an outbox are not rejected; `ReopenGoal` returns any
  closed goal to `InProgress`. `SetGoalDueDate` sets or clears an
  optional `dueOn`.
- Deleting a container deletes its live children in the same command and
  transaction (`adr/0042`, extended to reference items by `adr/0047`):
  `DeleteArea` → its lists → their tasks and reference items;
  `DeleteTaskList` → its tasks and reference items. Each child emits its own
  `TaskListDeleted` / `TaskDeleted` / `ReferenceItemDeleted`; the
  list-to-children step is `TaskListCascade`. `CreateTask`, `MoveTaskToList`
  and `OrganiseInboxItem` reject a missing or deleted target list, or a
  target list whose `Kind` is not `Tasks`; `CreateReferenceItem` and
  `MoveReferenceItemToList` reject a missing or deleted target list, or one
  whose `Kind` is not `Reference` (`TaskList.RequireAcceptsTasks` /
  `RequireAcceptsReferences`). `CreateTaskList` and `MoveTaskListToArea`
  reject a missing or deleted target area. An Inbox item only ever becomes a
  task — it never organises into a `Reference` list.
- List order is presentation, not domain (`adr/0051`). `TaskList` carries
  no position; `AreaView` (Presentation module) holds a user's `Order` of
  one area's lists, created by the first `ReorderLists` and addressed by the
  deterministic `AreaView.IdFor(userId, areaId)`. `ReorderLists` carries the
  order the client displays plus the moved list and target index, so the
  module never reads `TaskList`; it rejects an empty area, a list missing
  from the order, duplicates and another user's view, and emits nothing when
  the order is unchanged. Display order is `Arranged.Sort`: ids in `Order`
  first, then every other list by `createdAt`, then id; stale ids are
  ignored, so deleting or moving a list never touches a view and area
  deletion leaves its view orphaned (account deletion sweeps it).
  `TaskList.CreatedAt` is backfilled from `TaskListCreated` at startup.
- `ReferenceItem` (`adr/0047`) is its own aggregate, not a kind of task: a
  `Name`, Markdown `Description`, `Starred`, dense `Position` and ordered
  `ReferenceField(Id, Label, Value, Display?, Position)`. `Display` is an
  opaque hint string the domain never interprets — `Text`/`Link`/`Path`/
  `Quantity` is a client rendering concern (§4 of `specs/ui-spec.md`).
  Commands: `CreateReferenceItem`, `RenameReferenceItem`,
  `SetReferenceItemDescription`, `StarReferenceItem`, `MoveReferenceItemToList`,
  `DeleteReferenceItem`, `AddReferenceField`, `EditReferenceField`,
  `MoveReferenceField`, `RemoveReferenceField`. Field ordering reuses
  `Positions` (dense, rewritten range), the same mechanism as steps.
- `TodoTask.Description` (Markdown, `SetTaskDescription`) is rendered
  client-side only (`adr/0048`) — the domain stores and moves a plain string,
  never parses it.
- Sharing (`adr/0054`): five list commands — `ShareTaskList` (set/rotate
  the invite token and 6-character code, owner-only; the invite lives 30
  minutes, `adr/0057`), `StopSharingTaskList` (clear it, owner-only,
  members stay), `RemoveListMember` (owner-only), `LeaveTaskList`
  (member-only, rejects the owner), `JoinTaskList` (server-only, idempotent
  for the owner or an existing member; token and code compared in constant
  time, a wrong code commits `InviteCodeRejected`, the fifth closes the invite). `ListAccess` admits the owner or a
  member to every content command on tasks, steps, reference items and
  their fields, plus `CreateTask`, `CreateReferenceItem`,
  `OrganiseInboxItem`, and `LinkTaskToGoal`; list-level commands
  (`RenameTaskList`, `DeleteTaskList`, `MoveTaskListToArea`, the five
  sharing commands) stay owner-only through `TaskList.Require`.
  `LinkTaskToGoal` goes through `ListAccess` like any content command, but
  `TodoTask.Decide` itself rejects a non-owner actor — goals are the
  owner's, so the gate lives in the aggregate's rule rather than in the
  loader. Every event carries the owner as `UserId`; `ActorId` records who
  actually acted.
  `MoveTaskToList` and `MoveReferenceItemToList` reject a target list whose
  owner differs from the source list's — a task or item never crosses
  ownership by moving. Organising an Inbox item into a list the actor is a
  member of is allowed: it becomes a task owned by the list's owner, while
  the Inbox itself stays the actor's own aggregate.
- Placement of a shared list is presentation too. `ListView`
  (Presentation module) holds one user's `AreaId?` for a list they are a
  member of, set by `PlaceList`; `null` leaves it in "Shared with me", a
  client-side virtual area, never an `Area` document. A view naming a
  deleted or unknown area, or no view at all, falls back to "Shared with
  me" the same way a missing `AreaView` falls back to creation order. A
  filed list joins that area's `AreaView.Order` through `ReorderLists` like
  any other list. `GET /api/today` and the client's Today projection both
  include tasks from lists the caller is a member of, same `TodayRule`, the
  viewer's own time zone.
- Snapshot marks (`adr/0056`): `TodoTask.SnapshotMarks` and
  `ReferenceItem.SnapshotMarks` each hold `SnapshotMark(SnapshotId,
  StepId?, MarkedAt)` — a note that a public-snapshot visitor ticked this
  entry, never a state change. `MarkTaskFromSnapshot` /
  `MarkReferenceItemFromSnapshot` are `IServerOnlyCommand`s run by
  `PSPad.Module.Sharing` with the list owner as `UserId`; they add or
  remove one mark, reject a deleted task or an unknown step, and emit
  nothing when the mark is unchanged. `ClearTaskSnapshotMarks` /
  `ClearReferenceItemSnapshotMarks` are ordinary owner-or-member commands
  that dismiss every mark a task (steps included) or item carries. A mark
  never completes a task, checks a step, ticks an occurrence or enters
  `TodayRule.Plan` — it is purely a chip the owner sees; Statistics ignores
  every one of these events, by omission from its projections' `switch`.

---

## 5. Sync

`GET /api/sync?since={seq}&full={listIds}` returns every aggregate document
the caller can see with `seq > since` — owned, plus `tasklists` the caller
is a member of and `todotasks`/`referenceitems` whose `listId` or
`previousListId` (the list a moved row last left) is a list the caller is a
member of — the events in that range (still `userId == caller` only; the
client does not read them), and the new marker. `full` is a comma-joined
list of ids the caller is a member of: those lists and their live children
come back in full regardless of `since`, letting a device that just joined,
or a second device of an already-joined user, catch up on a membership it
has no delta history for. The response also carries `memberListIds` — the
live lists the caller belongs to and does not own — on every call, not only
a `full` one. The client overwrites its replica with what it receives — the
server is truth, the replica is disposable (AD-6).

**Reconciling membership.** `SyncService` saves the delta, then — only when
the replica has a recorded owner and the response carries
`memberListIds` — reconciles against it: a list in the replica not owned by
the caller and missing from `memberListIds` is purged along with its tasks
and reference items; one rule covers leaving, removal, the owner deleting
the list, and the owner deleting their account. A list in `memberListIds`
missing from the replica is pulled in the same pull, by asking again with
`full=` at the just-saved marker. A device holding nothing yet for this user
has no recorded owner, so reconciliation is skipped entirely — every list
would otherwise look foreign. A failed full pull (`HttpRequestException`) is
swallowed and never written to the marker; the missing list stays missing
and the next sync retries it, instead of every delta pull wedging behind a
flaky connection. Last, a task or reference item the caller does not own
whose `ListId` names a list the replica does not hold is dropped — a row the
owner moved out of a shared list, delivered through `previousListId` as a
stub (`id`, `userId`, `listId`, `previousListId`, `version`, `deleted`,
`seq` only — never its name, description, steps, fields, dates or marks). `IReplica.LoadAllAsync<T>()` returns every row of a type —
a replica holds one user's whole visible world, not only what that user
owns — and `IReplica.RemoveAsync` deletes one row; `replica.js`'s `getAll`
reads a type's rows with the key range `bound([type], [type, []])`.

`POST /api/lists/join {token, code}` resolves an invite token to a list,
checks the code with `TaskList.CheckInvite`, runs `JoinTaskList` through the
normal pipeline as the caller, and on acceptance returns
`200 JoinListResponse(ListId, Documents)` — that list and its live children,
via the same reader `full` uses. The owner or an existing member gets `200`
without a code check. A blank token or code, an unknown or cleared token, a
deleted list, a wrong code (which still runs `JoinTaskList` so the strike is
recorded) and an expired invite with a wrong code all return the same empty
`404`; only a right token and right code on an expired invite return `410`.
The endpoint carries the `"join"` rate-limit policy: 10 attempts per user
(subject claim) per 30-minute fixed window, then `429`. The client's
`JoinAsync` maps these to `JoinOutcome` (`Joined`, `Invalid`, `Expired`,
`TooManyTries`) and writes a `Joined` response straight into the replica.

A member's copy of a `tasklists` row — in `/api/sync` and in the join
response — never carries `inviteToken`, `inviteCode`, `inviteExpiresAt`,
`wrongCodes` or `closedByWrongCodes`; `SyncReader` strips them from every
list the caller does not own.

`POST /api/commands` takes a batch of command envelopes from the outbox, in
order, and returns one result per envelope. Rejections surface to the user,
never dropped silently (AD-5): `SyncCoordinator` raises the Rejected status
belt and keeps the messages for Settings → Sync (`specs/ui-spec.md` §2). Conflicts resolve last-write-wins per
aggregate; the loser gets a rejection carrying a reason. The outbox ships
in order and stops on the first rejection, so a failed command can't be
overtaken by one that depended on it.

**Every accepted command triggers an immediate sync**, not just the
60-second poll / reconnect / app-start triggers. `CommandSender` calls
`ISyncTrigger.SyncNowAsync()` once a command is accepted — fire-and-forget,
since the local write is already durable. `SyncNowAsync` is the single
guarded entry point every trigger goes through: overlapping calls coalesce
into one in-flight run plus exactly one guaranteed follow-up pass, so a
poll tick landing mid-burst never double-pushes the outbox. `Start()`
always hands back its own fresh pull rather than a possibly-stale
in-flight one, because `AuthorizeRouteView` mounts the shell once
signed-out and once signed-in and both must each see their own data land.

A sync revision cascades from `AppShell` so every replica-backed screen
redraws when new data lands; the shell waits for the first pull to
complete before rendering content on a device holding nothing yet for this
user, rather than flashing an empty state.

The client stores a fingerprint of the sync collection names it knows
(`SyncService.Collections.Keys`, sorted, joined) in replica meta alongside
the marker. If the stored fingerprint is missing or differs from the
client's current one — an already-installed client that only just updated
to a build with a new sync collection — the next pull asks `since = 0`
once instead of the stored marker, so documents in that new collection are
not silently skipped forever by a marker that had already advanced past
them (`adr/0049`). `areaviews` (`adr/0051`) and `listviews` (`adr/0054`) are
such collections.

**App updates are offered, never forced** (`adr/0040`). The published
service worker keeps the browser's waiting state — no `skipWaiting()` on
install, no `clients.claim()` — and only skips waiting when a page posts it
`{ type: 'SKIP_WAITING' }`. `js/updates.js` watches the registration
(a worker already waiting at start, or `updatefound` → `installed` while a
controller exists), calls `registration.update()` hourly and whenever the
tab becomes visible, and reports through `IAppUpdates` (`BrowserAppUpdates`,
announcing once). A `controllerchange` the tab did not ask for — another tab
applied the update — is announced too. `ApplyAsync` posts the message and reloads on
`controllerchange`. The outbox lives in IndexedDB, so the reload loses
nothing that was accepted locally.

---

## 6. Identity & session

Keycloak is the only sign-in path. The API validates JWT bearer tokens
against the realm; the client uses authorization code with PKCE. No header
identity seam anywhere — the `sub` claim maps to a `User`, everything else
keys off `userId`. `Program.cs` sets `MapInboundClaims = false` so claim
names read as Keycloak sends them (`name`, not `ClaimTypes.Name`).

On first sign-in the API provisions the user: a `User` with the time zone
the client reports (browser IANA zone, editable later), one Inbox, and a
small seed of areas. Provisioning is idempotent, runs in a transaction.
`SetUserDisplayName` heals an already-provisioned user's display name when
the token's name differs from the stored one (`MeEndpoints` issues it on
every `/api/me` call), so a Keycloak profile change picks up on next
sign-in with no migration.

**A durable local session, not the live access token, gates whether the
app opens.** Two different questions were previously conflated: "who is
this device's user" (answerable offline) and "may I call the API right
now" (only answerable online, and only matters at call time).

| | `LocalSession` | Access token |
|---|---|---|
| Answers | Who is this device's user? | May I call the API right now? |
| Storage | IndexedDB | `sessionStorage`, library-managed |
| Lifetime | Until logout or the trust window lapses | Minutes |
| Readable offline | Yes | Irrelevant offline |
| Gates | Opening the app | API calls only |

`LocalSession` holds the user id, display name, email, time zone, the OIDC
refresh token, and `LastServerContactUtc`. A custom
`LocalAuthenticationStateProvider` builds its principal from this record,
not from the library's token store.

**Boot sequence:**

```
index.html paints the brand-mark splash
  ↓
WASM boots — splash held, no chrome rendered
  ↓
read LocalSession from IndexedDB
  ├── none ─────────────────────────► login
  ├── stale (now − LastServerContactUtc > 7 days)
  │     └── clear replica + LocalSession, keep outbox ──► login
  └── fresh
        ├── render app from replica; the ready shell tears down the splash
        └── background, non-blocking: refresh token
              ├── success → store rotated token, update LastServerContactUtc, /api/me, start sync
              ├── transport failure/timeout/5xx → offline indicator, retry on reconnect
              └── 400 invalid_grant → clear LocalSession → login
```

The offline trust window is **7 days**: any successful refresh or sync
resets it, so normal use never expires; a lost device stops serving
readable local data after a week of no contact. On expiry the replica is
cleared (server-derived, disposable) but the **outbox survives** (locally
authored intent that exists nowhere else). A `400 invalid_grant` from the
token endpoint is authoritative revocation and clears the session; any
transport failure is not — it keeps the session and just surfaces offline
state, because conflating the two would sign a user out every time they
open the app in a tunnel.

Token renewal is a direct `POST` to
`{Authority}/protocol/openid-connect/token` with `grant_type=refresh_token`,
on an `HttpClient` with no authorization handler attached — no iframe, no
cross-origin cookie dependency (which mobile browsers block in standalone
PWA mode). The library's own automatic-silent-renew mechanism is never
armed: its `IRemoteAuthenticationService`/`IAccessTokenProvider` is never
resolved outside the interactive `/authentication/*` routes, so two
renewal mechanisms never race against one rotating refresh token. Own
token attachment goes through a `DelegatingHandler`, not the library's
`AuthorizationMessageHandler`.

`/api/me` refreshes identity opportunistically when online — it never
gates readiness. Its failure sets an offline indicator and nothing else.

Slow Wi-Fi answers nothing rather than failing, so every client request
carries a stall limit of 10 seconds: the token endpoint through its
`HttpClient.Timeout`, the API through `StalledRequestHandler`, which turns
a request with no response headers by then into an `HttpRequestException` —
the same transport failure the sync loop and `ServerReachability` already
treat as offline. The limit covers the wait for headers, not the body, so a
large first pull on a slow link is not cut off mid-download.

**Sign-out** ends the Keycloak session directly (not just local state).
Signed-out visitors land on the public `/welcome` screen, never a bare
redirect. The Keycloak realm sets `ssoSessionIdleTimeout` (30 days) and
`ssoSessionMaxLifespan` (90 days) explicitly — the idle timeout must exceed
the 7-day local trust window, or the server invalidates the refresh token
before the client-side window becomes the effective bound.

**Account deletion** (`DELETE /api/account`, see `adr/0034`) is the one
operation in the system that is not an `ICommand`. It can't run offline or
through the outbox — there is nothing left to sync a queued "delete
everything" against — and no aggregate owns "all of a user's data," so it
does not fit the load/decide/apply/stage shape §2 describes. It is handled
directly in `PSPad.Api`:

1. Wait for the domain event pump to handle every envelope published before
   the request began (`ChannelDomainEventDispatcher.DrainAsync`, capped at
   10 seconds, `adr/0041`). Otherwise a projection still in flight writes
   `statistics_*` documents back after the wipe.
2. In the same transaction, before the sweep: pull the caller out of
   `_members` on every other owner's `tasklists` document they belong to
   (`$pull` by `userId`), stamping each with a fresh `seq` so those owners'
   next sync carries the departure. The caller's own lists are untouched
   here — they are deleted outright by the sweep below, same as everything
   else they own.
3. In one MongoDB transaction, enumerate every collection in the database
   (`Database.ListCollectionNames()`) and run
   `DeleteMany({ userId: callerId })` against each — including `events` and
   `processed_commands`. No collection name is hardcoded, so a new aggregate
   added later (a habit, a yearly goal) is covered with no code change here —
   `list_snapshots` and `snapshot_visits` (`adr/0056`) needed none either,
   since both carry `userId` like every other collection; only
   `tasklists._members`, a nested array rather than a document of its own,
   needed the explicit `$pull` in step 2.
4. Only once that transaction commits, call Keycloak's Admin REST API
   (`DELETE /admin/realms/{realm}/users/{sub}`), authenticating with the
   existing bootstrap master-realm admin credentials
   (`KEYCLOAK_ADMIN_USER`/`KEYCLOAK_ADMIN_PASSWORD`) — no new realm client
   or service account. Mongo data is deleted first: if the Keycloak call
   then fails (retried a couple of times inline), the failure mode is an
   orphaned, empty Keycloak login an admin can clean up manually, never
   surviving personal data with nowhere left to request its own deletion.
   The response is `200 { keycloakRemoved: true }` on full success, or still
   `200 { keycloakRemoved: false }` after the retries are exhausted — the
   data is already irreversibly gone either way, so the client's job is to
   report which parts finished, not to fail the request. A failure before
   the Mongo transaction commits is the only case that returns a non-2xx,
   and it means nothing was deleted.
5. There is no local password to check (no local password store exists at
   all — Keycloak is the only sign-in path, §6 above), so the client-side
   confirmation is a typed-email match, not a password prompt. That is a UX
   safeguard against misclicks, not the authorization boundary — the caller's
   own validated JWT `sub` is, and the handler only ever deletes that id.
6. The caller is authenticated but the operation still requires
   connectivity; it does not go through the offline session model in the
   table above.

---

## 7. HTTP surface

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/commands` | Execute a batch of commands. The only write endpoint |
| `GET` | `/api/sync?since=&full=` | Delta pull, widened to lists the caller is a member of; `full` (comma-joined list ids the caller belongs to) returns those lists and their children in full regardless of `since` |
| `POST` | `/api/lists/join` | Join a shared list by invite token and code. `200 JoinListResponse(ListId, Documents)`; one empty `404` for any bad token, wrong code or deleted list; `410` when token and code match an expired invite; `429` past 10 attempts per user per 30 minutes. Not through the outbox — see §5 |
| `GET` | `/api/today` | Server-side Today, for a cold client; includes tasks from lists the caller is a member of, in the caller's own time zone |
| `GET` | `/api/statistics/records?before=&limit=` | The statistics feed, newest first. `limit` clamps to 1..200, default 50 |
| `GET` | `/api/statistics/overview?days=` | Tiles and the five chart series. `days` is 30, 90 or 365, default 30 |
| `GET` | `/api/me` | Current user; provisions on first call, heals display name |
| `PUT` | `/api/me/timezone` | Set the user's IANA time zone (not through the offline command path — rare, server-owned, online-only) |
| `DELETE` | `/api/account` | Delete the caller's account: every Mongo document scoped to their `userId`, then their Keycloak user. Not a command — see §6 |
| `POST` | `/api/lists/{id}/snapshots` | Owner only. Publish a frozen copy; `{expiresAt}` → `{id, token, expiresAt}`. Online-only, like `/api/me/timezone` (`adr/0056`) |
| `GET` | `/api/lists/{id}/snapshots` | Owner only. That list's active (unexpired) snapshots |
| `DELETE` | `/api/snapshots/{id}` | Owner only. Revoke a snapshot |
| `POST` | `/api/me/snapshot-visits` | Record that the caller opened a snapshot by token |
| `GET` | `/api/me/snapshot-visits` | The caller's unexpired visits, newest first — the List snapshots tab |
| `GET` | `/api/public/snapshots/{token}` | **Anonymous.** The frozen snapshot, or `404` for an unknown or expired token — indistinguishable by design |
| `POST` | `/api/public/snapshots/{token}/marks` | **Anonymous.** `{entryId, stepId?, marked}` — tick or untick an entry; saved on the snapshot, then run against the owner's task through a server-only command |
| `GET` | `/health` | Liveness, unauthenticated |

Writes go through one endpoint because every write is a command and the
outbox ships them in batches — splitting per feature would buy nothing and
make ordering harder to honour. The two `/api/public/*` routes are the one
exception: they sit in their own `AllowAnonymous` route group
(`/api/public`), rate-limited by a fixed window per IP (60 requests/minute
by default, overridable through `Sharing:PublicRequestsPerMinute` — a code
default for tests, not a documented self-hoster setting) rather than by
authentication, since there is no caller identity to limit by. Behind a
reverse proxy every visitor would share the proxy's address, so the API
relies on ASP.NET Core's built-in `ASPNETCORE_FORWARDEDHEADERS_ENABLED`
(compose maps it from `API_BEHIND_PROXY`, default `false`): the host's
startup filter puts `UseForwardedHeaders` (`X-Forwarded-For`,
`X-Forwarded-Proto`, any proxy trusted, last hop only) ahead of the whole
pipeline, `UseRateLimiter` included, so `RemoteIpAddress` is the visitor's.
Off by default because with port 5000 open directly a caller could forge the
header.

---

## 8. Containers

Two images, each with a `Dockerfile` beside its `.csproj`, built with the
repo root as context.

- `src/PSPad.Api/Dockerfile` — ASP.NET runtime, port 8080.
- `src/PSPad.App/Dockerfile` — publishes the WASM output into
  `nginx:alpine`; nginx serves pre-compressed assets, sets the
  `application/wasm` MIME type, falls back to `/index.html` for client
  routes.

The client is static, so its API base URL and Keycloak settings can't be
baked in — the image ships `wwwroot/appsettings.json` as a template plus an
entrypoint substituting environment variables at container start.
Because that rewrite happens after the build, the published service worker
never pins `appsettings.json` to its build-time integrity hash — a pinned hash
fails the worker's install and strands every client on the old build. It
answers the file from its cache and refreshes it in the background
(stale-while-revalidate), so a network that accepts the request and never
answers cannot hold the boot; a rewritten value takes effect from the second
launch after a redeploy (ADR-0044).

nginx caches only fingerprinted `_framework/` files as `immutable`; every
other script, stylesheet and JSON file (`js/replica.js` included) is served
`no-cache`, so a new build never runs against a stale module. `replica.js`
still opens a database a newer build already upgraded at its existing
version (stores are only ever added) and closes its connection on
`versionchange`, so one tab never blocks another tab's upgrade.

`docker/compose.yaml` runs MongoDB and Keycloak for development.
Integration tests never touch it — they start their own MongoDB via
Testcontainers. `docker/compose.prod.yaml` runs api, app, mongo and
keycloak behind a reverse proxy terminating TLS.

---

## 9. Statistics

`PSPad.Module.Statistics` is a bounded context, not a view over `events`.
It owns four collections (`statistics_records`, `statistics_inbox_records`,
`statistics_labels`, `statistics_state`, all in §3) and writes them only from domain events —
there are no Statistics commands and no write endpoint. Current task state
(`TodoTask`) is never read to decide what a chart shows; only
`statistics_records` is.

**Event dispatch.** `MongoUnitOfWork.CommitAsync` publishes the events it
just committed, as `DomainEventEnvelope(Seq, DomainEvent)`, through
`IDomainEventDispatcher` **after** `CommitTransactionAsync` returns — never
inside the transaction. The default `ChannelDomainEventDispatcher` writes to
a bounded, single-reader `Channel<DomainEventEnvelope>`. A dispatch failure
at publish time is caught and logged; the write already committed, so it
never fails an accepted command (`adr/0036`).

**The pump and replay.** One `DomainEventPump : BackgroundService` drains
the channel. Before draining anything, it resolves `IDomainEventReplay` in
its own DI scope and calls `CatchUpAsync`, which reads
`statistics_state.lastProcessedSeq`, replays `events` forward from there in
batches of 500 through every registered `IDomainEventHandler`
(`StatisticsRecordProjection`, `StatisticsLabelProjection`,
`InboxRecordProjection`), and advances
the marker after each batch. Only after replay finishes does the pump start
draining live envelopes, resolving a fresh scope and every handler per
batch. A handler that throws is caught and logged — one bad handler cannot
take the pump down — but **the marker is written only by replay, never by
the live drain loop**: if the live loop advanced it too, a swallowed
handler failure would be permanent and silent. Leaving it untouched means
every event dispatched live is replayed again from the marker on the next
boot, whether or not its handler already succeeded — the deliberate cost of
`adr/0036`.

`DomainEventCatalogue` (mirroring `CommandCatalogue`) maps a stored event's
type name back to its CLR type for replay's JSON deserialization.

**Records are immutable and idempotent by construction.**
`StatisticsRecord.Id` is `"{seq}:{userId}"` (`adr/0055`) — not the bare `seq`,
since a shared event can produce two records, one per user it concerns — so a
duplicate dispatch or a replay upserts over the same row rather than
duplicating it. `StatisticsRecordView.Id` on the wire is still the `long`
`Seq`, unchanged for the client's `before=` paging.

**Two records when owner and actor differ.** `StatisticsRecordProjection`
writes one record when `ActorId == UserId`, as before `adr/0054`. Otherwise
it writes the owner's record (`Role: Owner`) and a second, actor-facing copy
(`Role: Actor`): same `Seq`, `UserId` set to the actor, `GoalId` cleared,
`CompletionNumber` recomputed against the actor's own completions of that
task. `statistics_inbox_records` never gets an actor copy — the Inbox is
never shared. Labels resolve by id (`ILabelStore.ByIdsAsync`), not by the
caller's `userId`, so a member's feed can still name a list or goal that
belongs to the owner. `StatisticsCharts.Outstanding`, its tiles
(`NetChange` included) and `ByGoal` all restrict to `Role: Owner` records —
a member's own "outstanding" line and net change must not move because they
completed someone else's task, and a task's goal stays the owner's business.
See `adr/0055`.

**A versioned projection rebuilds once when its shape changes.**
`statistics_state.projectionVersion` (absent = 1) is compared against
`StatisticsProjection.Version` on every start
(`IProjectionMarker.IsBehindAsync`). An older or absent version first drops
`statistics_records`, `statistics_inbox_records` and `statistics_labels`
(`IStatisticsReset.ClearAsync`) and recreates their indexes, and only then
resets `lastProcessedSeq` to 0 and records the new version
(`AdoptVersionAsync`) — so a clear that fails, or a host that dies between
the two, leaves the old version and the next start clears again. Then the
normal replay path (above) rebuilds every row from `events` alone. No
migration script; the log is the source (`adr/0055`).

**Record shape and enrichment.** `StatisticsRecord` (fields in §3) is built
from its source event's payload alone — no live lookup at projection time.
`TaskCompleted`, `TaskReopened`, `TaskDeleted`, `TaskMovedToList`,
`TaskLinkedToGoal` and `OccurrenceCompleted` all carry `Name` (and
`ListId`/`GoalId`/`DueOn` where relevant) for this reason; `TaskCreated`
already did. Events stored before this enrichment deserialize with those
fields empty — `StatisticsReader` falls back to `ITaskSnapshotSource` for
the task's current name, or renders `(deleted task)` if the task is gone.
`OccurrenceCompleted` is a toggle (`bool Completed`): ticking a recurring
day and un-ticking it produce two records, `OccurrenceTicked` and
`OccurrenceUnticked` — never an update to the first. A chart that needs the
current tick state resolves each `(TaskId, OccurrenceDay)` pair to its
highest-`Seq` record. `statistics_labels` is a separate projection
(`AreaCreated`/`Renamed`/`Deleted`, `TaskListCreated`/`Renamed`/`Deleted`,
`GoalCreated`/`Renamed`/`Deleted`) holding the current name of every area,
list and goal without Statistics ever reading `Tasks`' own collections. Two
readers consume it: the feed resolves a record's `ListId` and `GoalId`, and
`ByGoal` resolves goal names. `Area` labels are projected but read by nothing
today — they are kept because the projection is fed by the same event stream
and a future area breakdown would otherwise start with no history.
`TaskListMovedToArea` is not projected, since a label carries a name, not a
parent. See `adr/0037` for why this is a read-side projection and not a
second audit trail alongside `events`.

**Charts derive only from `statistics_records`** (`StatisticsCharts`, in
`PSPad.Module.Statistics/Charts/`), never from `TodoTask`, so a deleted or
renamed task never rewrites what a past day's chart showed:

- Completions per day, split planned/unplanned.
- Tasks opened per day.
- Outstanding-open count per day — the burndown chart's server-side
  successor, replacing the deleted `BurndownRule` (`adr/0019`). A task is
  open iff its latest lifecycle record (`Created`, `Reopened`, `Completed`,
  `Deleted`) is `Created` or `Reopened` — not a signed running total, which
  would double-decrement a task that is completed and then deleted.
  Lifecycle records are ordered by `(bucketed day, Id)`, not `Id` alone,
  because an offline-authored command can commit days after it was created
  and so carry a higher `seq` than a same-day command issued elsewhere;
  ordering by `seq` alone would apply it to the wrong day's running total.
- Work by goal, ranked, with an explicit "No goal" bar.
- A consistency heatmap — one cell per day, shaded by completion count.
- Inbox captures per week — `StatisticsCharts.Captures`, one `WeeklyCount` per
  week the window touches, counting the items **created in the Inbox that
  week**. A per-week tally, not a running level: it needs no opening balance,
  because a capture older than the window belongs to a week the window does
  not show. An item captured and then organised or discarded still counts in
  the week it was captured — what became of it afterwards changes no bar.
  Days are bucketed in the user's time zone, and weeks start Sunday, the same
  convention the consistency grid's Sunday accent uses. The window's first
  week is partial by construction: it counts only its days from the window's
  first day on.

**Inbox captures are their own projection and their own collection.**
`InboxRecordProjection` maps `InboxItemCaptured` — and nothing else — to an
`InboxRecord` (`_id` = the event's `seq`, so replay upserts) in
`statistics_inbox_records`, read through `IInboxRecordStore` (`SaveAsync`,
`SinceAsync`). It is deliberately not a `StatisticsRecord`: that record is
task-shaped (`TaskId`, `TaskName`, `ListId`, `GoalId`, `CompletionNumber`,
`ITaskSnapshotSource` status resolution) and an inbox item is not a task, so
folding captures into it would put rows with no task in the record feed and
force every existing chart's kind filter to be re-audited (`adr/0037`).
`InboxItemOrganised` and `InboxItemDiscarded` are deliberately **not**
projected: nothing reads them once the chart counts arrivals, and a row no
reader consumes is a write that can only rot — emptying the Inbox stays in
`events`, where a future metric would rebuild it from `seq` 0 like every other
statistics row (`adr/0039`). `StatisticsOverviewReader` reads the captures
window with the **same instant** it passes to `SinceAsync` and
`OpenTaskIdsBeforeAsync`, so the inbox chart and the task charts never
disagree about where the window starts.

**Cross-module read.** Current task status for the feed's `CurrentStatus`
column comes through a port Statistics declares and `PSPad.Api` implements:
`ITaskSnapshotSource.CurrentAsync(taskIds)` → `TaskSnapshot(TaskStatus,
Name)`, one batched call per feed page, never per row. Statistics never
touches `IDocumentStore<TodoTask>` directly.

## 10. Out of scope

Habits, annual plans, integrations, the print domain, push reminders, the
thought of the day — unchanged from AGENTS.md §3. Reference lists and task
descriptions have shipped (`adr/0047`, `adr/0048`); a per-list typed field
schema and other list kinds beyond `Tasks`/`Reference` remain out. Retention
or archival of `statistics_records` —
unbounded, same as `events` (§3, §11). Mid-session token renewal beyond the
on-demand refresh described in §6. Offline sign-in for a device that has
never signed in — impossible, the first token exchange requires Keycloak.

## 11. Open

Retention for the event log, recurrence occurrences and `statistics_records`
— unbounded, or archived per year. Deferred until there is enough data to
measure.
