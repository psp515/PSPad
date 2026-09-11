# PSPad Slice 1 — GTD Core Design

**Date:** 2026-09-11
**Status:** approved for planning
**Covers:** subsystems 1 (GTD core), 2 (action history), 3 (identity) from `AGENTS.md`

---

## 1. Purpose

Deliver the foundation of PSPad: a self-hosted, multi-user GTD system where a
single Today screen answers "what do I do now" across every area of life, capture
is frictionless through one shared Inbox, and the whole thing keeps working on a
phone with no network.

Everything else in `AGENTS.md` §8 builds on this slice. Nothing else is in it.

---

## 2. Glossary

| Term | Meaning |
|------|---------|
| **Area** | A top-level division of life: life, work, studies, projects. User-defined. |
| **List** | A named collection of tasks inside exactly one area. |
| **Inbox** | One per user, outside all areas. Holds unprocessed capture items. |
| **Inbox item** | Raw captured text. Not a task yet. Becomes one when organized into a list. |
| **Task** | A unit of work in exactly one list. Has name, due date, priority, star, goal link, steps. |
| **Step** | A checkable item inside a task, with its own due date. Ordered. |
| **Next step** | The first unchecked step of a task, by order. Shown next to the task name. |
| **Goal** | A named intent, global across areas. Many tasks may point to one goal. |
| **Recurring task** | A template plus generated per-day occurrences. |
| **Occurrence** | One day's instance of a recurring task. States: pending, done, skipped. |
| **Today** | A computed cross-area view, see §5. |

---

## 3. Domain model

### 3.1 Aggregates

| Aggregate | Identity | Contains | Notes |
|-----------|----------|----------|-------|
| `User` | `UserId` | display name, time zone | Created on first sign-in. Owns everything else. |
| `Area` | `AreaId` | name, order, archived flag | Deleting is archiving. |
| `TaskList` | `ListId` | name, `AreaId`, order, archived flag | Named `TaskList`, not `List`, to avoid clashing with `System.Collections.Generic.List`. |
| `TodoTask` | `TaskId` | name, `ListId`, due date, priority, starred, `GoalId?`, steps, recurrence?, completion | Steps live inside. Named `TodoTask` to avoid clashing with `System.Threading.Tasks.Task`. |
| `Goal` | `GoalId` | name, description, archived flag | Global — no `AreaId`. |
| `Inbox` | `UserId` | items | Exactly one per user. |

Cross-aggregate links (`TodoTask.ListId`, `TodoTask.GoalId`, `TaskList.AreaId`)
are ids, never nested objects.

### 3.2 Ownership and isolation

Every aggregate carries `UserId`. Every query filters by the signed-in user.
There is no sharing between users in this slice.

### 3.3 Invariants

**Area**
- Name is non-empty, trimmed, max 100 chars.
- Name is unique per user among non-archived areas.

**TaskList**
- Name non-empty, trimmed, max 100 chars.
- Belongs to an existing, non-archived area.
- Name unique within its area among non-archived lists.

**TodoTask**
- Name non-empty, trimmed, max 200 chars.
- Belongs to an existing, non-archived list.
- Priority is one of `None`, `Low`, `Medium`, `High`. Default `None`.
- `Starred` is a boolean. It affects sorting only — never Today membership.
- `GoalId` either null or an existing, non-archived goal.
- Due date is a `DateOnly` or null.
- Steps are ordered by an integer position, dense from 0.
- A step has non-empty text (max 200), an optional `DateOnly` due date, and a
  `CompletedAt` instant or null.
- A non-recurring task may be completed; completing it does not complete its steps.
- A recurring task is never completed directly — its occurrences are. See §6.
- A task cannot be both recurring and have a due date; recurrence supplies the dates.

**Goal**
- Name non-empty, trimmed, max 200 chars. Description optional, max 2000.

**Inbox**
- An item has non-empty text, max 500 chars, and a capture timestamp.
- Organizing an item removes it from the Inbox and creates a task in a chosen
  list, in one command.

### 3.4 Priority

```csharp
public enum Priority { None = 0, Low = 1, Medium = 2, High = 3 }
```

Sort order on any list screen: starred first, then priority descending, then due
date ascending (nulls last), then name.

---

## 4. Functional core

The domain is written as pure decide/apply functions so the same code runs on the
server and inside WebAssembly (AD-3, AD-4):

```csharp
public static IReadOnlyList<IDomainEvent> Decide(TState state, TCommand command);
public static TState Apply(TState state, IDomainEvent @event);
```

- `Decide` validates against the current state and returns the events that
  should happen, or throws `DomainException` with a machine-readable code.
- `Apply` is total: it never validates and never throws.
- Neither touches the clock, randomness, I/O, Marten, or HTTP. The current
  instant and any new id arrive as fields on the command, stamped by the caller.

This is what lets the client apply a command optimistically to its local replica
and the server replay the identical command against the real aggregate.

---

## 5. Today rule

A task appears on Today when **either**:

1. its due date is today or earlier, **or**
2. its next unchecked step has a due date that is today or earlier.

"Today" means today in the **user's time zone**, stored on `User`. The server
never uses machine-local time.

A task whose relevant date is strictly before today is **overdue** and sorts
above everything else. Within the overdue group and within the on-time group the
sort is §3.4's.

**Recurring tasks never become overdue.** Only today's occurrence appears, and
only while it is pending. A missed occurrence stays on its own day as *skipped*
and never migrates forward. "Read a book" untouched yesterday does not show as
overdue today.

The Today view shows, per row: task name, next unchecked step (if any), area
name, priority, star, and the date that put it there.

---

## 6. Recurrence

A recurring task holds a `RecurrenceRule`:

```csharp
public sealed record RecurrenceRule(
    RecurrenceKind Kind,        // Daily | DaysOfWeek | EveryNDays
    int Interval,               // EveryNDays only; >= 1
    DayOfWeek[] DaysOfWeek,     // DaysOfWeek only; non-empty
    DateOnly StartDate,
    DateOnly? EndDate);
```

`RecurrenceRule.Occurs(DateOnly day)` is a pure predicate. It is the single
source of truth for whether a day is scheduled.

Occurrences are **not** pre-generated into the far future. An occurrence record
is created lazily the first time a scheduled day is observed — when Today is
computed for that day, or when the user acts on it. Each occurrence is
`(TaskId, Date, Status, CompletedAt?)` with `Status` in `Pending | Done | Skipped`.

Completing today's occurrence emits `OccurrenceCompleted`. There is no command to
skip: any past scheduled day with no `Done` occurrence *is* skipped, derived, not
stored. This keeps the write model small and gives habits (subsystem 4) its
streak history for free.

---

## 7. Commands and events

Commands and events are records in **`PSPad.Domain`** — a decide function takes a
command and returns events, so all three are one layer. `PSPad.Contracts` sits
above the domain and holds only the wire shapes: the command envelope with its
type discriminator, the sync request/response, and the read-model DTOs. Both
projects are referenced by client and server alike.

Every command carries `UserId`, `CommandId` (client-generated GUID, used for
idempotency), and `IssuedAt` (UTC instant stamped by the issuer).

| Command | Events |
|---------|--------|
| `CreateArea` | `AreaCreated` |
| `RenameArea` | `AreaRenamed` |
| `ArchiveArea` | `AreaArchived` |
| `CreateList` | `ListCreated` |
| `RenameList` | `ListRenamed` |
| `ArchiveList` | `ListArchived` |
| `CaptureToInbox` | `InboxItemCaptured` |
| `OrganizeInboxItem` | `InboxItemOrganized`, `TaskCreated` |
| `DeleteInboxItem` | `InboxItemDeleted` |
| `CreateTask` | `TaskCreated` |
| `RenameTask` | `TaskRenamed` |
| `SetTaskDueDate` | `TaskDueDateSet` |
| `SetTaskPriority` | `TaskPrioritySet` |
| `StarTask` / `UnstarTask` | `TaskStarred` / `TaskUnstarred` |
| `LinkTaskToGoal` / `UnlinkTaskFromGoal` | `TaskLinkedToGoal` / `TaskUnlinkedFromGoal` |
| `MoveTaskToList` | `TaskMovedToList` |
| `AddStep` | `StepAdded` |
| `EditStep` | `StepEdited` |
| `CompleteStep` / `UncompleteStep` | `StepCompleted` / `StepUncompleted` |
| `ReorderSteps` | `StepsReordered` |
| `RemoveStep` | `StepRemoved` |
| `CompleteTask` / `ReopenTask` | `TaskCompleted` / `TaskReopened` |
| `SetRecurrence` / `ClearRecurrence` | `RecurrenceSet` / `RecurrenceCleared` |
| `CompleteOccurrence` / `UncompleteOccurrence` | `OccurrenceCompleted` / `OccurrenceUncompleted` |
| `CreateGoal` | `GoalCreated` |
| `EditGoal` | `GoalEdited` |
| `ArchiveGoal` | `GoalArchived` |

Every event carries `UserId`, `OccurredAt`, and the `CommandId` that produced it.
That last field is what makes the action-history screen possible without a
separate audit table (AD-2) and what makes command replay idempotent.

---

## 8. Read models

Marten projections, all filtered by `UserId`:

| Projection | Purpose |
|------------|---------|
| `AreaTree` | Areas with their lists and open-task counts. Sidebar. |
| `TaskListView` | Tasks of one list: name, next step, due date, priority, star, goal name. |
| `TodayView` | Rebuilt per request from `TaskDateIndex` — see below. |
| `TaskDateIndex` | One row per task: earliest relevant date (task due or next step due), recurrence rule, archived/completed flags. Today queries hit this. |
| `InboxView` | Inbox items, newest first. |
| `GoalView` | Goals with linked open/done task counts. |
| `HistoryView` | Flattened event stream: when, what, which entity, which command. Filterable by date, area, entity kind. |

`TodayView` is computed, not stored, because "today" depends on the requesting
user's clock. `TaskDateIndex` is stored and makes that computation a single
indexed query.

---

## 9. Offline and sync

### 9.1 Client state

The client keeps in IndexedDB:

- `replica` — the read models it has pulled, keyed by type and id.
- `outbox` — commands issued while the local state was ahead of the server.
- `meta` — the last server version marker seen, and the last sync timestamp.

### 9.2 Write path

1. User acts. The client builds a command, stamping `CommandId` and `IssuedAt`.
2. The client runs the domain's `Decide`/`Apply` against its replica and writes
   the resulting state back. The UI updates immediately.
3. The command is appended to the outbox.
4. A background sync loop drains the outbox in order, `POST`ing to
   `/api/commands` as a batch.
5. The server rejects a command it has already seen by `CommandId` — replay is
   safe.

### 9.3 Read path

`GET /api/sync?since={version}` returns every read-model row changed after that
version marker, plus the new marker. The client overwrites those rows in its
replica. Server state always wins (AD-5, AD-6).

### 9.4 Conflicts

A command whose `Decide` fails server-side is stored in a `rejected` list with
its reason and surfaced in the UI as a dismissible item ("This task no longer
exists"). It is never dropped silently and never retried automatically.

### 9.5 Version marker

A monotonic `long` from a Postgres sequence, bumped on every projection write.
Not a timestamp — clocks on client devices are not trusted.

---

## 10. Identity

- ASP.NET Core Identity for built-in username/password accounts.
- OpenID Connect to Keycloak as an alternative sign-in, configured by
  environment variables. Both paths land on the same local `User` aggregate,
  matched on the OIDC subject claim.
- The WASM client authenticates with a bearer token; the API validates it.
- First sign-in creates the `User` aggregate, an empty Inbox, and four seeded
  areas (life, work, studies, projects) which the user may rename or archive.

---

## 11. Runtime and environment

Everything runs in Docker, including the SDK (user decision):

- `.devcontainer/` — a dev container built on `mcr.microsoft.com/dotnet/sdk:10.0`,
  where builds, tests, and `dotnet watch` all run.
- `docker/compose.yaml` — `postgres`, `postgres-test`, `keycloak`, and the app.
- `docker/compose.prod.yaml` — production images for app + postgres + keycloak.
- No host machine dependency beyond Docker itself.

Server tests run against the real `postgres-test` service, not an in-memory
fake — Marten's behavior *is* the thing under test.

---

## 12. Testing strategy

| Layer | How |
|-------|-----|
| Domain | xUnit, pure functions, no I/O. Every invariant in §3.3 gets a test. The Today rule (§5) and recurrence (§6) get exhaustive cases including the "recurring never overdue" case. |
| Handlers + projections | xUnit against `postgres-test`. Command in, events and projection rows out. |
| Sync | Round-trip test: issue commands offline, drain the outbox, assert server state and the returned delta. |
| Client | bUnit for components whose logic is non-trivial (Today row rendering, next-step display). No broad UI test suite. |

---

## 13. Out of scope for this slice

Habits, annual plans and yearly summaries, charts, push reminders, thought of the
day, GitHub/OneDrive/Google Drive/Thingiverse integrations, print material and
part lists, reference-material lists, checklist templates, list types beyond
plain, sharing between users, mobile native apps.

---

## 14. Global constraints

- .NET 10 (LTS). `net10.0` target for all projects.
- `PSPad.Domain` and `PSPad.Contracts` reference **no** infrastructure packages
  and must compile for WebAssembly.
- PostgreSQL 17. Marten 8.x. Wolverine 4.x. MudBlazor 8.x. xUnit + Shouldly.
- All dates that a user sees are `DateOnly` in the user's time zone. All
  instants stored are UTC `DateTimeOffset`.
- No audit table. History comes from the event stream.
- Code, comments, commits, and documentation in English.
- GPL v3 — every dependency must be compatible.
