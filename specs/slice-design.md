# Slice 1 — Design

Supersedes the deleted `2026-09-11-gtd-core-design.md`. Same product, different
substrate: MongoDB instead of PostgreSQL, three feature modules instead of one
`PSPad.Domain`, a separately hosted client.

## 1. Goal

Deliver the GTD core, action history and identity as one running system: a
.NET 10 API over MongoDB, a Blazor WebAssembly PWA served by nginx, Keycloak for
sign-in. The Today screen answers "what do I do now" across every area, the Inbox
captures without friction, and the client keeps working with no network.

## 2. What changed, and what it costs

| Was | Is | Consequence |
|-----|----|-------------|
| Marten on PostgreSQL 17 | MongoDB 8, single-node replica set | No migrations. No event store either — we write the log ourselves |
| Event stream is the source of truth | Aggregate documents are the source of truth; events are an append-only log beside them | History still comes only from events, and no audit table exists. But state is never rebuilt by replay |
| Sync marker = Marten event sequence | Sync marker = our own monotonic `seq` | Same property: ordered, resumable, never a clock |
| Wolverine | An `ICommandHandler<T>` interface and DI | No durable outbox on the server. The client outbox (IndexedDB) is untouched, and that is the one that mattered |
| `PSPad.Domain` | `PSPad.Module.Tasks` (plus `.History`, `.Identity`) | The purity guard retargets |

AD-2 is amended, not dropped: **action history is the event log, and nothing else
writes history**. AD-3, AD-4, AD-5, AD-6, AD-7 and AD-8 stand as written.

## 3. Project layout

```
PSPad.slnx
src/
  PSPad.Api/                   Minimal API, endpoints per slice, DI composition, Dockerfile
  PSPad.App/                   Blazor WASM PWA, MudBlazor, IndexedDB replica + outbox, Dockerfile (nginx)
  shared/
    PSPad.Abstractions/        ICommandHandler<T>, IDocumentStore<T>, IEventLog, IClock, IAggregate
    PSPad.Contracts/           wire shapes: command envelope, sync DTOs, history DTOs
    PSPad.Infrastructure/      Mongo client, connection string, generic repository, Keycloak/JWT, DI registration
  modules/
    PSPad.Module.Tasks/        areas, lists, Inbox, tasks, steps, goals, recurrence, Today — pure, WASM-safe
    PSPad.Module.History/      queries over the event log
    PSPad.Module.Identity/     User, time zone, first-sign-in provisioning
test/
  PSPad.Module.Tasks.Tests/    unit only
  PSPad.Api.Tests/             integration, Testcontainers MongoDB
  PSPad.App.Tests/             unit + bUnit
  PSPad.TestInfrastructure/    Mongo fixture, category attributes, architecture guards
docker/                        compose files, keycloak realm, nginx config
```

### References, one direction only

| Project | May reference |
|---------|---------------|
| `PSPad.Abstractions` | nothing |
| `PSPad.Contracts` | `PSPad.Abstractions` |
| `PSPad.Module.Tasks` | `PSPad.Abstractions` — and nothing else. This is the whole purity rule |
| `PSPad.Module.History` | `PSPad.Abstractions`, `PSPad.Contracts` |
| `PSPad.Module.Identity` | `PSPad.Abstractions`, `PSPad.Contracts` |
| `PSPad.Infrastructure` | `PSPad.Abstractions`, `PSPad.Contracts` — never a module |
| `PSPad.Api` | everything |
| `PSPad.App` | `PSPad.Module.Tasks`, `PSPad.Abstractions`, `PSPad.Contracts` — never `PSPad.Infrastructure` |

`PSPad.Infrastructure` stores documents generically by `T`, so it never needs to
know that a module exists. BSON conventions are registered once at API startup,
which keeps the MongoDB driver out of the aggregates.

`PSPad.App` references `PSPad.Module.Tasks` because the same command handler runs
in the browser against the IndexedDB replica and on the server against MongoDB.
That is AD-3, and it is the only reason offline edits and server state agree.

## 4. Command pipeline

One shape, both sides of the wire.

```csharp
public interface IAggregate
{
    Guid Id { get; }
    Guid UserId { get; }
    int Version { get; }
}

public interface ICommand
{
    Guid CommandId { get; }
    Guid UserId { get; }
}

public abstract record DomainEvent(Guid AggregateId, Guid UserId, DateTimeOffset At);

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

Aggregates keep the rules pure and testable with no store at all:

- `Decide(command)` returns the events the command produces, or throws
  `DomainRejectedException` carrying a reason.
- `Apply(event)` folds one event into state.

A handler does exactly four things — load, decide, apply, stage. It never names a
database type; it depends on two ports:

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

Writing is separate from reading because one command can touch two aggregates —
organising an inbox item creates a task and empties the item — and both have to
land together or not at all.

On the server `CommitAsync` persists every staged aggregate, appends their
events, bumps the sync sequence and records the command id **in one MongoDB
transaction**. In the browser the same call writes the IndexedDB replica and
appends to the outbox.

`Aggregate` carries one settable `long Seq`, stamped by the write with the
sequence of the last event that touched it. It is data about the write, never
about the domain, and no `Decide` may read it.

### Idempotency

Every command carries a `CommandId` generated by the client. The server records
processed ids in `processed_commands` inside the same transaction as the write. A
replayed command — the outbox shipping twice after a flaky reconnect — finds its
id present and returns the earlier result without writing. This is the only
defence the outbox needs.

## 5. Storage

**MongoDB 8, single-node replica set `rs0`.** The replica set is not optional:
multi-document transactions require one. Development, production and tests all
run the same shape.

### Collections

| Collection | Holds | Notable fields |
|-----------|-------|----------------|
| `users` | one per person | `timeZone` (IANA), `provisionedAt` |
| `areas` | user-defined areas | `name`, `position` |
| `lists` | task lists, each inside one area | `areaId`, `name`, `position` |
| `inboxes` | one per user | `items[]` |
| `tasks` | tasks with their steps inline | `listId`, `dueOn`, `goalId`, `priority`, `starred`, `steps[]`, `recurrence`, `completedDays[]` |
| `goals` | global goals | `name`, `horizon` |
| `events` | the action history and the sync feed | `seq`, `userId`, `aggregateType`, `aggregateId`, `type`, `payload`, `at` |
| `processed_commands` | idempotency keys | `_id` = command id, `at` |
| `counters` | the global sequence | `_id: "events"`, `value` |

Every aggregate document carries `_id` (GUID), `userId`, `version` (optimistic
concurrency), `seq` (the sequence of the last event that touched it) and
`deleted` (soft delete, so a removal can travel through sync).

### The sequence

`counters` holds one document. Each write runs
`findOneAndUpdate({_id: "events"}, {$inc: {value: 1}})` inside the transaction,
and the returned value stamps both the event and the aggregate's `seq`. It is
monotonic and safe as a resume marker, which a clock is not. Contention is one
document per installation — for a self-hosted GTD app, nothing.

### Indexes

- `events`: `{userId: 1, seq: 1}` and `{userId: 1, at: -1}` for the history screen
- every aggregate collection: `{userId: 1, seq: 1}` for delta sync
- `tasks`: `{userId: 1, listId: 1}`, `{userId: 1, dueOn: 1}`
- `lists`: `{userId: 1, areaId: 1}`
- `processed_commands`: TTL index on `at`, 30 days

## 6. Domain rules

### Today

A task belongs on Today when its due date is today or earlier, **or** its next
unchecked step is due today or earlier. Earlier means overdue, and overdue pins
to the top. "Today" is today in the *user's* time zone, read from
`users.timeZone` — never machine-local time. The rule takes the instant and the
time zone as arguments, so it is deterministic under test.

### Recurrence never goes overdue

A recurring task stores a rule plus the days it was completed
(`completedDays`). Pending and Skipped are **derived** from the rule and today,
never stored. Only today's occurrence can appear on Today, and only while it is
pending. A missed day stays behind as skipped and never migrates forward. "Read a
book" untouched yesterday must not show up overdue today.

The rule is tested in the module (unit), in the API's Today query (integration)
and in the client projection (bUnit). If a change breaks it, three suites go red.
That redundancy is deliberate.

### Ordering

Lists, steps and inbox items order by a dense `position` integer. Reordering
rewrites the affected range rather than using fractional keys — the ranges are
short and the rewrite touches one document.

## 7. Sync

`GET /api/sync?since={seq}` returns every aggregate document for the caller with
`seq > since`, the events in the same range, and the new marker. The client
overwrites its replica with what it receives: the server is truth, the replica is
disposable (AD-6).

`POST /api/commands` takes a batch of command envelopes from the outbox, in
order, and returns one result per envelope. Rejections surface to the user and
are never dropped silently (AD-5). Conflicts resolve last-write-wins per
aggregate, and the loser gets a rejection carrying a reason.

The outbox ships in order and stops on the first rejection, so a failed command
cannot be overtaken by one that depended on it.

## 8. Identity

Keycloak is the only sign-in path. The API validates JWT bearer tokens against
the realm; the client uses authorization code with PKCE. There is no header
identity seam at any point — the `sub` claim maps to a `User`, and everything
else keys off `userId`.

On first sign-in the API provisions the user: a `User` with the time zone the
client reports (the browser's IANA zone, editable later), one Inbox, and a small
set of seed areas. Provisioning is idempotent and runs in a transaction.

## 9. HTTP surface

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/commands` | Execute a batch of commands. The only write endpoint |
| `GET` | `/api/sync?since=` | Delta pull |
| `GET` | `/api/today` | Server-side Today, for a cold client |
| `GET` | `/api/history?before=&limit=` | Action history, newest first |
| `GET` | `/api/me` | Current user; provisions on first call |
| `GET` | `/health` | Liveness, unauthenticated |

Writes go through one endpoint because every write is a command and the outbox
ships them in batches. Splitting them per feature would buy nothing and make
ordering harder to honour.

## 10. Containers

Two images, each with a `Dockerfile` beside its `.csproj`, built with the repo
root as context.

- `src/PSPad.Api/Dockerfile` — ASP.NET runtime, port 8080.
- `src/PSPad.App/Dockerfile` — publishes the WASM output into `nginx:alpine`.
  nginx serves the pre-compressed assets, sets the `application/wasm` MIME type
  and falls back to `/index.html` for client routes.

The client is static, so its API base URL and Keycloak settings cannot be baked
in. The image ships `wwwroot/appsettings.json` as a template plus an entrypoint
that substitutes environment variables into it at container start.

`docker/compose.yaml` runs MongoDB and Keycloak for development. Integration
tests never touch it — they start their own MongoDB through Testcontainers.
`docker/compose.prod.yaml` runs api, app, mongo and keycloak behind a reverse
proxy that terminates TLS.

## 11. Testing

Every test class carries `[UnitTest]` or `[IntegrationTest]` from
`PSPad.TestInfrastructure`; both emit the xUnit trait `Category`. An unmarked test
class fails the build, enforced by a guard test.

**Unit** — pure, in-process, no Docker, no network. Domain rules, the Today rule,
recurrence derivation, client state folding, bUnit components.

**Integration** — a real MongoDB via Testcontainers, a real HTTP host. Handlers,
transactions, idempotency, the Today query, history, the sync round trip, auth.

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

One container per test run, shared by an xUnit collection. Tests isolate by using
a distinct `UserId` each, so documents never collide and nothing has to be
dropped between tests.

The MongoDB fixture must start the container **with a replica set** — without one
every transaction fails. If the Testcontainers MongoDB module's `WithReplicaSet()`
is unavailable in the installed version, fall back to a raw container running
`--replSet rs0` with an `rs.initiate()` exec at startup.

### Architecture guards

1. `PSPad.Module.Tasks` and `PSPad.Abstractions` reference no infrastructure
   assembly — no `MongoDB.*`, no `Microsoft.AspNetCore.*`, no `System.Net.Http`.
2. `PSPad.Infrastructure` references no module assembly.
3. Every test class carries a category trait.

All three are tests, not documentation.

## 12. Out of scope

Habits, annual plans, integrations, the print domain, reference materials,
charts, push reminders, thought of the day, list types beyond plain. Unchanged
from AGENTS.md §3.

## 13. Open

Retention for the event log — unbounded, or archived per year. Deferred until
there is enough data to measure.
