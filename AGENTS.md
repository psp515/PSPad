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
| Store | **Marten on PostgreSQL 17** | Documents + event sourcing one engine; history falls out of event stream, no audit table; ACID; one container |
| Messaging | **Wolverine** | Native Marten integration, handler + event append one unit of work |
| Frontend | **Blazor WebAssembly standalone, PWA** | Blazor Server needs live connection, offline is hard requirement |
| UI kit | **MudBlazor** | Complete component set out of box; Material look accepted over shadcn (React-only, Blazor ports immature) |
| Local store | IndexedDB | Offline replica + command outbox |
| Identity | ASP.NET Core Identity + OIDC to Keycloak | Self-hosted, multi-user |
| Integration tests | **Testcontainers** | Real Postgres per run, disposable, no shared fixture state |
| Packaging | Docker Compose | App, Postgres, Keycloak |

---

## 5. Architecture decisions

**AD-1 — Modular monolith, vertical slices.** Features are folders (Areas, Inbox,
Tasks, Goals, Today, History), each holds its commands, handlers, projections,
endpoints. No Services/Repositories layering. No microservices.

**AD-2 — CQRS, event-sourced write side.** Commands mutate aggregates, append
events. Marten projections build read models. Queries never touch aggregates.
Action history **is** the event stream. Never write a parallel audit log.

**AD-3 — Commands are shared contract.** Command and event types live in
`PSPad.Domain`, referenced by WASM client and server. Client applies command to
IndexedDB replica immediately, records in outbox, ships on reconnect. Server
replays same command through real aggregate. One model of behavior, not two.

**AD-4 — Domain compiles to WASM.** Aggregates and rules carry no Marten, no
HTTP, no infrastructure. Purity guard test enforces it.

**AD-5 — Offline conflicts: last-write-wins per aggregate.** Rejected commands
surfaced to user, never dropped silent. Data single-owner, real conflicts rare.
CRDTs rejected: merge engine costs more than whole GTD core.

**AD-6 — Sync is delta-by-version.** Client pulls rows changed since its marker,
overwrites replica. Server is truth, replica disposable. Marker = Marten event
sequence, never a clock.

**AD-7 — Recurrence is template + occurrences.** Never one task with rolling
date. Only Done days stored; Pending and Skipped derived from rule + today. Gives
habits streaks free.

**AD-8 — Aggregates:** `User`, `Area`, `TaskList`, `TodoTask` (steps inside),
`Goal`, `Inbox` (one per user). Cross-aggregate links are ids, never nested
objects.

**AD-9 — Integration tests own their database.** Testcontainers starts a real
Postgres 17 per test run. No compose service shared with dev. No in-memory fake —
Marten behavior is the thing under test.

---

## 6. Repo layout (planned)

```
src/
  PSPad.Domain/          aggregates, commands, events, rules — WASM-safe, no infrastructure
  PSPad.Contracts/       wire shapes: command envelope, sync DTOs
  PSPad.Server/          Wolverine handlers, Marten projections, endpoints, vertical slices
  PSPad.Client/          Blazor WASM PWA, MudBlazor, IndexedDB replica + outbox
test/
  PSPad.Domain.Tests/         unit only, no I/O
  PSPad.Server.Tests/         integration, Testcontainers Postgres
  PSPad.Client.Tests/         unit + bUnit component tests
  PSPad.TestInfrastructure/   Testcontainers fixture, trait constants
docs/
  superpowers/specs/     design specs, one per subsystem
  superpowers/plans/     implementation plans
docker/                  compose: app, postgres, keycloak
```

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
[Collection(PostgresCollection.Name)]
public class SomethingTests(PostgresFixture fixture);
```

**Unit** — pure, in-process, no Docker, no network, no filesystem. Domain rules,
projections-as-functions, client state folding, bUnit components.

**Integration** — real Postgres via Testcontainers, real Marten, real HTTP host.
Command handlers, projections, Today query, history, sync round trip, auth.

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
public sealed class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("pspad_test")
        .WithUsername("pspad")
        .WithPassword("pspad")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
```

Isolation between tests: each test uses its own `UserId`, so rows never collide.
Do not truncate tables between tests — it serializes the suite for no gain.

### Testcontainers inside the dev container

Dev container needs the host Docker socket, otherwise Testcontainers cannot start
anything:

```json
"mounts": ["source=/var/run/docker.sock,target=/var/run/docker.sock,type=bind"]
```

Compose still runs dev Postgres and Keycloak for running the app. Tests do not
touch those.

---

## 8. Current step

Slice 1 specified and planned. Nothing implemented.

- Spec: `docs/superpowers/specs/2026-09-11-gtd-core-design.md`
- Plans: `docs/superpowers/plans/`, see README there for order

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
- Everything in Docker including SDK — dev container, not just infra
- Offline-first PWA, which killed Blazor Server

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

**Keep domain pure.** Infrastructure dependency inside `PSPad.Domain` breaks AD-3
and AD-4. Find another way.

**Never add an audit table.** History comes from events (AD-2).

**Every test gets a category trait** (§7). Unmarked test is a broken test.

**Language:** code, comments, commits, docs in English. Chat with maintainer may
be Polish.

**License:** GPL v3. Dependencies must be compatible.
