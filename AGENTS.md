# PSPad — Agent Guide

Self-hosted multi-user GTD notepad. Entry point for any agent in repo.

**Status: slice 1 implemented** through plan 09 (responsive UI shell). Repo
holds `src/`, `test/`, `specs/`, `adr/`, `docker/`.

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
| 8 | Analytics & reminders — push, thought of the day (a burndown chart already ships in slice 1's History screen) | later |

4 and 5 cheap later because slice 1 models recurrence as occurrences and goals as
entities. Do not regress that.

---

## 3. Slice 1 scope

In: areas (user-defined), lists (inside one area), Inbox (one per user, outside
areas, organizing = first-class command), tasks (one list, name + due date +
goal + priority + star + steps), steps (own due date, ordered, dense positions),
recurrence (template + per-day occurrences), goals (global, many tasks to one),
Today screen (cross-area), action history, a burndown chart on the History
screen, offline PWA, auth.

Out: habits, annual plans, integrations, print lists, reference materials,
push reminders, thought of day, list types beyond plain.

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
brand/                   icon.svg — the single icon master
docs/                    Astro documentation site, published to GitHub Pages
specs/                   design specs — ui-spec.md and backend-spec.md are the
                         standing rulebooks; superseded design narratives
                         live in git history, not the working tree
adr/                     architecture decision records + index and template
.superpowers/sdd/        working plans for in-flight features (not committed)
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
Mongo startup is seconds, per-test is unusable. Pin the image tag (`mongo:8`,
never `latest`) — an upstream bump changing behavior under you is a worse bug
than the one you were testing for.

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

### Hosted (`WebApplicationFactory`) tests

Every test that boots the API host goes through `ApiFactory`, never a bare
`new WebApplicationFactory<Program>()`. `Program.cs` runs
`MongoIndexes.EnsureAsync` before `app.Run()`, so *any* hosted test needs a
real, reachable Mongo — there is no health-check-only path that skips it.
A bare factory falls through to `appsettings.json`'s dev connection string,
which is absent in CI and misleadingly present on a dev box that happens to
have Mongo running locally — the test passes for the wrong reason and fails
the moment it runs somewhere else.

```csharp
public sealed class ApiFactory(MongoFixture fixture) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = "pspad_test"
            }));

        return base.CreateHost(builder);
    }
}
```

Use `ConfigureAppConfiguration`, not `ConfigureHostConfiguration`. `Program.cs`
uses the minimal-hosting model (`WebApplication.CreateBuilder`, no
`Startup`/`IWebHostBuilder`), so `WebApplicationFactory<Program>` runs it
through a deferred host builder: host configuration is merged *before*
`Program.cs`'s own config sources (`appsettings.json` included), so anything
added via `ConfigureHostConfiguration` gets silently overridden by
`appsettings.json`. `ConfigureAppConfiguration` layers on top instead and
actually wins. Every test class that needs the host joins the shared
`[Collection(MongoCollection.Name)]` and takes `MongoFixture` — one factory
per test method is fine (cheap), one container is not (shared via the
fixture).

### Running tests

Develop on the host with the .NET 10 SDK. Docker must be running, because
integration tests start their own MongoDB. They never touch the compose stack —
that one exists for running the app.

---

## 8. Current step

Slice 1 is built and merged: foundation, tasks core, recurrence and Today, API
and persistence, identity, history, PWA client, offline sync, responsive UI
shell. The numbered plans that drove it are gone; two standing rulebooks
replaced the narrative design specs that drove the work (`slice-design.md`,
`offline-first-session-design.md`, `ui-ux-redesign-design.md`,
`ui-redesign-2-design.md`, `ui-polish-design.md` — their content lives in
`git log -- specs/`, not the working tree, and the ADRs they produced stay in
`adr/`):

- `specs/ui-spec.md` — component choice, layout and spacing, page structure,
  visual/theming and navigation/auth screen shapes. Authoritative for
  anything touching the client's UI.
- `specs/backend-spec.md` — project layout, command pipeline, storage and
  index shapes, domain rules, sync, identity and session, HTTP surface,
  containers. Authoritative for anything below the UI.

Both are rulebooks describing current behaviour, not history. `adr/0012`
(ordering module) is still `Proposed` and still unbuilt. `adr/0022` (drawer
cleanup: dead account-menu arrow removed, Settings and App info as sidebar
rows, area actions moved to a FAB) and `adr/0023` (search pulled from the
sidebar for now, drawer footer with date/time and license) are `Active` and
built on this branch. `adr/0027` (a durable local session, not the access
token, gates the app) is `Active` and built on this branch too; it amends
`adr/0024`'s consequence, since the login-callback route now renders branded
fragments rather than the library's default text. `adr/0028` (sign-out ends
the Keycloak session directly; signed-out visitors land on a public `/welcome`
screen) is `Active` and built on this branch as well, and amends `adr/0027`'s
sign-out consequence. `adr/0029` (a sync revision cascades from `AppShell` so
replica-backed screens redraw when data lands) is `Active` and built here too,
as is `adr/0030` (the shell waits for the first pull on a device holding
nothing for this user), which amends it, and `adr/0031` (every `Start` hands
back its own pull, because `AuthorizeRouteView` mounts the shell once signed
out and once signed in), which supplies the mechanism 0030 needed. `adr/0034`
(account deletion bypasses the command pipeline for a generic, `userId`-swept
cross-collection wipe, Mongo first then the Keycloak user, confirmed
client-side by typing the account's own email rather than a password) is
`Active` and built on this branch too.

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

**Design before code.** New feature goes brainstorming, spec, plan. A spec
that outlives its plans is committed to `specs/<topic>-design.md`; in-flight
plans live under `.superpowers/sdd/<feature>/` and are not committed.

**Specs are a knowledge source, not history.** Before work on a subsystem,
read the spec that covers it — `specs/backend-spec.md` for anything touching
the command pipeline, storage shape, domain rules, sync, identity/session or
the HTTP surface; `specs/ui-spec.md` for anything touching the client's
component choice, layout, page structure, theming or navigation. They answer
*what the intended behaviour is* at a level the code does not state and
AGENTS.md only summarises. Where a spec and an ADR disagree, the ADR wins —
it is the decision of record; where a spec and the code disagree, say so
rather than silently following either.

**Docs ship with the change.** A change to what a self-hoster runs (compose
services, environment variables, secrets, ports) updates
`docs/src/pages/install.astro`; a change to what the application does (a new
screen, a new capability, scope moving from *later* to *now*) updates
`features.astro` and the landing page. Same piece of work, never a follow-up.
The compose block and the environment table are read from
`docker/compose.yaml` and `docker/.env.example` at build time and need no hand
edit — but a new variable needs its `#` description comment in `.env.example`,
and the prose around them is hand-written.

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

**Prefer MudBlazor components over custom markup.** Before adding a new
`pspad-*` CSS class or a hand-rolled `<div>` layout, check whether a
MudBlazor component already does it — grids are `MudGrid`/`MudItem`, cards
and bordered containers are `MudPaper`. A custom class is for things
MudBlazor genuinely has no component for (brand marks, page-specific
theming), not a substitute for one that exists. See `specs/ui-spec.md` for
the fuller ruleset.

**Never add an audit table.** History comes from events (AD-2).

**Every test gets a category trait** (§7). Unmarked test is a broken test.

**One type per file, grouped by operation.** Filename matches the
class/record/enum it contains (standard C# convention). Inside each
aggregate's folder, one command's command/event/handler live together in a
verb-named subfolder — `Goals/Achieve/AchieveGoal.cs`,
`Goals/Achieve/GoalAchieved.cs`, `Goals/Achieve/AchieveGoalHandler.cs` — so
a whole operation is one folder, not three files scattered across
`XCommands.cs`/`XEvents.cs`/`XHandlers.cs`. Elements the whole aggregate
shares (the aggregate class itself, e.g. `Goals/Goal.cs`) stay directly in
the aggregate's folder, not inside any verb subfolder. A sub-aggregate
(steps inside a task) nests one level deeper: `Tasks/Steps/Add/AddStep.cs`.
The namespace stays the aggregate's namespace regardless of nesting depth
(`PSPad.Module.Tasks.Goals`, not `...Goals.Achieve`) — folders are for
navigation, not for the type system.

**Read the ADRs before touching architecture.** Before any task that
touches module boundaries, persistence, sync, contracts, or aggregate
shape, read `adr/README.md`'s index and the `Active` records it
points to — not just AGENTS.md §5. §5 is a terse summary; the ADR carries
the context and rejected alternatives that explain *why*, which is what
keeps a "cheaper-looking" alternative from silently re-opening a settled
tradeoff.

**Architecture decisions go in `adr/`.** One file per decision,
using `adr/template.md`'s format (title, tags, date, status,
context, decision, alternatives, consequences). AGENTS.md §5 stays the
terse day-to-day summary (AD-1 … AD-9); the ADR is where the reasoning and
rejected alternatives live. Changing your mind about a past decision never
edits an old ADR's Decision or Consequences — write a new one that
supersedes it and update the old one's status line.

**Any new or changed architectural decision gets an ADR, immediately.**
If a task makes, changes, or supersedes an architectural decision — not
just implements one already on record — add or update the ADR (and its
row in `adr/README.md`'s index) in the same piece of work, before
calling it done. Don't defer this to a follow-up. If the decision also
shifts an AD-N summary in AGENTS.md §5, update that line too so §5 and the
ADR set never drift apart.

**Language:** code, comments, commits, docs in English. Chat with maintainer may
be Polish.

**License:** GPL v3. Dependencies must be compatible.
