# PSPad — Agent Guide

Self-hosted multi-user GTD notepad. Entry point for any agent in repo.

**Status: design done, no code yet.** Repo holds license, .gitignore, empty
`src/`, `test/`, `docs/`, `docker/`.

---

## 1. Vision

Daily-driver GTD system, not generic todo app. One Today screen answers "what do
I do now" across all areas. Capture frictionless via one shared Inbox. Runs on
own hardware. Works on phone with no network, syncs when back.

Later: habits, yearly goals, connected accounts (GitHub, OneDrive, Google Drive,
Thingiverse), 3D-print material and part lists, reference libraries. Not slice 1.

---

## 2. Subsystems

Platform, not one project. Each gets own spec, plan, implementation.

| # | Subsystem | Slice |
|---|-----------|-------|
| 1 | GTD core — areas, Inbox, lists, tasks, steps, recurrence, goals, Today | **1 (now)** |
| 2 | Action history — event log + browse screen | **1 (now)** |
| 3 | Identity — built-in login + Keycloak (OIDC) | **1 (now)** |
| 4 | Habits — streaks, daily progress | later |
| 5 | Goals & annual plans — yearly horizon, year-end summary | later |
| 6 | Integrations — GitHub issues, OneDrive, Google Drive, Thingiverse | later |
| 7 | 3D-print domain — materials, parts, reference materials | later |
| 8 | Analytics & reminders — charts, push, thought of the day | later |

4 and 5 cheap later because slice 1 models recurrence as occurrences and goals as
entities. Do not regress that.

---

## 3. Slice 1 scope

In: areas (user-defined), lists (inside one area), Inbox (one per user, outside
areas, organizing = first-class command), tasks (one list, name + due date +
goal + priority + star + steps), steps (own due date, ordered, dense positions),
recurrence (template + per-day occurrences), goals (global, many tasks to one),
Today screen (cross-area), action history, offline PWA, auth.

Out: habits, annual plans, integrations, print lists, reference materials,
charts, push reminders, thought of day, list types beyond plain.

### Today rule

Task on Today when: due date today or earlier, OR next unchecked step due today
or earlier. Earlier = overdue, pinned top. "Today" = today in **user's time
zone**, stored on `User`. Never machine-local time.

**Recurring tasks never overdue.** Only today's occurrence shows, only while
pending. Missed occurrence stays on its own day as skipped, never migrates
forward. "Read a book" untouched yesterday must not show overdue today. Rule
lives in domain layer, one place, shared client and server.

---

## 4. Tech stack

| Layer | Choice | Why |
|-------|--------|-----|
| Runtime | .NET 10 (LTS) | One language client and server |
| Store | **MongoDB 8, single-node replica set `rs0`** | Documents, no migrations. Replica set not optional — transactions need it |
| Messaging | **None.** `ICommandHandler<T>` + DI | Wolverine existed only for Marten integration. Without Marten it buys nothing WASM can use |
| Frontend | **Blazor WebAssembly standalone, PWA** | Blazor Server needs live connection, offline is hard requirement |
| UI kit | **MudBlazor** | Complete component set out of box; Material look accepted over shadcn (React-only, Blazor ports immature) |
| Local store | IndexedDB | Offline replica + command outbox |
| Identity | OIDC to Keycloak, JWT bearer | Self-hosted, multi-user. No local password store |
| Integration tests | **Testcontainers** | Real MongoDB per run, disposable, no shared fixture state |
| Packaging | Docker Compose | One image per deployable: API and client, each with its own `Dockerfile` beside its `.csproj`. The client is static, served by nginx |

---

## 5. Architecture decisions

**AD-1 — Modular monolith, three modules.** `PSPad.Module.Tasks` (areas, Inbox,
lists, tasks, steps, goals, recurrence, Today), `PSPad.Module.History`,
`PSPad.Module.Identity`. Inside a module, features are folders holding their
commands, events, aggregate, handlers. No Services/Repositories layering. No
microservices.

**AD-2 — Aggregate documents are truth; events are the log beside them.**
Commands decide, aggregates apply, one transaction writes the document, its
events and the command id. Action history **is** that event log. Never write a
parallel audit table. State is not rebuilt by replay — that is the price of
dropping Marten, and it is paid knowingly.

**AD-3 — Commands are shared contract.** Command, event and aggregate types live
in `PSPad.Module.Tasks`, referenced by WASM client and server. Client runs the
same handler against its IndexedDB replica, records in the outbox, ships on
reconnect. Server runs it against MongoDB. One model of behavior, not two.

**AD-4 — Tasks module compiles to WASM.** Aggregates and rules carry no MongoDB,
no HTTP, no infrastructure — `PSPad.Module.Tasks` references `PSPad.Abstractions`
and nothing else. A purity guard test enforces it, and a second guard keeps
`PSPad.Infrastructure` from referencing any module.

**AD-5 — Offline conflicts: last-write-wins per aggregate.** Rejected commands
surfaced to user, never dropped silent. Data single-owner, real conflicts rare.
CRDTs rejected: merge engine costs more than whole GTD core.

**AD-6 — Sync is delta-by-version.** Client pulls rows changed since its marker,
overwrites replica. Server is truth, replica disposable. Marker = our own
monotonic `seq` from the `counters` document, never a clock.

**AD-7 — Recurrence is template + occurrences.** Never one task with rolling
date. Only Done days stored; Pending and Skipped derived from rule + today. Gives
habits streaks free.

**AD-8 — Aggregates:** `User`, `Area`, `TaskList`, `TodoTask` (steps inside),
`Goal`, `Inbox` (one per user). Cross-aggregate links are ids, never nested
objects.

**AD-9 — Integration tests own their database.** Testcontainers starts a real
MongoDB 8 replica set per test run. No compose service shared with dev. No
in-memory fake — transaction and serialization behavior is the thing under test.

---

## 6. Repo layout (planned)

```
PSPad.slnx               solution (XML format, not .sln)
src/
  PSPad.Api/             Minimal API, endpoints, DI composition + Dockerfile
  PSPad.App/             Blazor WASM PWA, MudBlazor, IndexedDB replica + outbox
                         + Dockerfile (nginx, static)
  shared/
    PSPad.Abstractions/  Aggregate, ICommand, DomainEvent, IDocumentStore<T>, IUnitOfWork, IClock
    PSPad.Contracts/     wire shapes: command envelope, sync DTOs, history DTOs
    PSPad.Infrastructure/Mongo client, connection string, generic repository, Keycloak/JWT
  modules/
    PSPad.Module.Tasks/    areas, lists, Inbox, tasks, steps, goals, recurrence, Today — WASM-safe
    PSPad.Module.History/  queries over the event log
    PSPad.Module.Identity/ User, time zone, first-sign-in provisioning
test/
  PSPad.Module.Tasks.Tests/     unit only, no I/O
  PSPad.Module.History.Tests/   unit only
  PSPad.Module.Identity.Tests/  unit only
  PSPad.Api.Tests/              integration, Testcontainers MongoDB
  PSPad.App.Tests/              unit + bUnit component tests
  PSPad.TestInfrastructure/     Mongo fixture, trait constants, architecture guards
docs/
  superpowers/specs/     design specs, one per subsystem
  superpowers/plans/     implementation plans
docker/                  compose files, keycloak realm, nginx config
```

References run one way only: `Tasks` sees `Abstractions` and nothing else;
`Infrastructure` never sees a module; `App` never sees `Infrastructure`; `Api`
sees everything and is the only place a module meets MongoDB.

---

## 7. Testing

### Categories

Every test class gets one category attribute. No exceptions. Both live in
`PSPad.TestInfrastructure` and emit the xUnit trait `Category`:

```csharp
[UnitTest]
[IntegrationTest]
```

Integration classes also join the shared container:

```csharp
[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SomethingTests(MongoFixture fixture);
```

**Unit** — pure, in-process, no Docker, no network, no filesystem. Domain rules,
the Today rule, recurrence derivation, client state folding, bUnit components.

**Integration** — real MongoDB via Testcontainers, real HTTP host. Command
handlers, transactions, idempotency, Today query, history, sync round trip, auth.

Run:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test
```

Unit suite must stay under a few seconds. If it needs Docker, it is mislabeled.

### Testcontainers fixture

One container per test run, shared by an xUnit collection. Not one per test —
Postgres startup is seconds, per-test is unusable.

```csharp
public sealed class MongoFixture : IAsyncLifetime
{
    readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:8")
        .WithReplicaSet()
        .Build();

    IMongoClient _client = null!;

    public string ConnectionString => _container.GetConnectionString();

    public IMongoClient Client => _client;

    public IMongoDatabase Database => _client.GetDatabase("pspad_test");

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _client = new MongoClient(ConnectionString);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

[CollectionDefinition(MongoCollection.Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "mongo";
}
```

The replica set is mandatory. Without it every transaction fails.

Isolation between tests: each test uses its own `UserId`, so documents never
collide. Do not drop collections between tests — it serializes the suite for no
gain.

### Running tests

Develop on the host with the .NET 10 SDK. Docker must be running, because
integration tests start their own MongoDB. They never touch the compose stack —
that one exists for running the app.

---

## 8. Current step

Slice 1 respecified on MongoDB and replanned. Nothing implemented.

- Spec: `docs/superpowers/specs/2026-09-12-slice-1-design.md`
- Plans: `docs/superpowers/plans/`, eight of them, see README there for order

Start plan 01, then numbered order. 02 and 03 are pure domain, parallelizable.
Each plan ends green — a plan is done or not, no half state.

---

## 9. Future order

1. Habits — reuse occurrence model, add streaks
2. Annual plans — give `Goal` a yearly horizon, year-end summary
3. Analytics — charts over completions and habit progress
4. Reminders — server scheduler + web push (VAPID); thought of the day
5. GitHub — issue-backed lists; first external sync, sets pattern
6. Cloud storage — OneDrive and Drive paths; reference-material lists
7. Print domain — materials, parts, Thingiverse
8. Checklists — reusable templates convertible to one-shot list

---

## 10. Settled

- Goals **global**, not per area — "new eating habit" spans areas
- Priority: fixed four — none, low, medium, high
- Star means **important only**. Sorts up. Never puts task on Today. Dates alone drive Today
- Recurrence in slice 1, forced by the never-overdue rule
- Develop on the host, deploy in containers. No dev container: each deployable gets its own `Dockerfile` next to its `.csproj`, built with the repo root as context
- Offline-first PWA, which killed Blazor Server
- MongoDB over PostgreSQL: no migrations, at the cost of Marten's event store. State is truth, events are the log beside it, history still comes only from them
- Wolverine dropped with Marten. A handler interface and DI cover what is left, and the same interface runs in WebAssembly

Open: retention for occurrences and events — unbounded or archive per year.

---

## 11. Rules for agents

**Design before code.** New feature goes brainstorming, spec, plan. Spec path:
`docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.

**TDD.** Failing test first. Today rule and recurrence rule are where bugs hurt
most.

**No comments in code.** Name things so comments are unnecessary. If a line needs
explaining, it needs renaming or extracting. Exception: a genuinely
counter-intuitive constraint gets one line saying *why*, never *what*. XML docs
only on public API others consume across projects.

**Be terse.** Short answers. No preamble, no "great question", no restating the
task back. Fragments fine. Say what changed, where, what broke. Sacrifice grammar
for brevity.

**No narration.** Do not announce tool calls or describe what you are about to
do. Do it, then report result.

**Report failures exact.** Quote the shortest decisive line of an error. Never
claim green without running the suite.

**Keep the Tasks module pure.** An infrastructure dependency inside
`PSPad.Module.Tasks` breaks AD-3 and AD-4. Find another way.

**Never add an audit table.** History comes from events (AD-2).

**Every test gets a category trait** (§7). Unmarked test is a broken test.

**Language:** code, comments, commits, docs in English. Chat with maintainer may
be Polish.

**License:** GPL v3. Dependencies must be compatible.
