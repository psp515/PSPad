# Testing Spec

Standing rulebook for how PSPad is tested. Read before writing or moving a test.

## Categories

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

## What a test asserts

A test asserts an observable result: a returned value, a decision (events or
a rejection), a stored document, an HTTP response, a sync delta, or what a
user sees and can do in the browser. It never asserts a CSS class, a DOM
shape, a MudBlazor internal, or a call recorded on a fake of one of our own
ports.

| Tier | Subject | Runs |
|---|---|---|
| Unit `[UnitTest]` | Pure rules: domain `Decide` → events or rejection; Today, recurrence, lead time, money values and readings, list view rules, positions; CSV parsing and import planning; client queries and text rules; offline outbox, replica and sync folding; logic-bearing components (parsing, date or time math, state transitions, keyboard handling); architecture, category, resource, localization and stylesheet guards | in process, no Docker |
| Integration `[IntegrationTest]` | A command or query through HTTP on `ApiFactory` with Testcontainers Mongo, asserting the response, documents, events and sync deltas: every handler, cascade, projection, reader, sharing and auth flow | Testcontainers |
| End-to-end | A user's flow in Chromium against the compose stack: pages, layout, navigation, panels, localization, offline and live updates; new steps locate by role, label or text | compose stack |

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test
```

Where a new test goes: a rule with edge cases is a unit test, a server effect
is an integration test, anything a user sees on a page is an end-to-end step.
TDD is unchanged: the failing test comes first, and for UI work it is an
end-to-end step. Unit suite must stay under a few seconds. If it needs Docker,
it is mislabeled.

Stubs in unit tests are allowed only at external boundaries (HTTP to Keycloak
or NBP, the JS runtime, the clock, connectivity) and only to assert the
client's resulting behaviour, never the outgoing request's shape.

Integration tests send commands with `PSPad.Api.Tests.Commands.Commands`
(`SignInAsync`, `TrySendAsync`, `SendAsync`, `SyncedAsync`).

Migration record: `specs/behaviour-tests-design.md`; decision ADR-0079.

**End-to-end** — Playwright in `test/e2e/`, outside the .NET solution, driving
the real application in Chromium against the compose stack with one Keycloak
user per test. Whole-scenario journeys only, twenty-two of them, under five
minutes. Design, harness and scenario list: `specs/e2e-testing-design.md`.
`npm test` in that folder runs it; `dotnet test` never sees it. A change to
what a user can do adds or extends a scenario; a change to markup or copy a
scenario reads fixes it in the same pull request (design §9).

## Testcontainers fixture

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

## Hosted (`WebApplicationFactory`) tests

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
uses the minimal-hosting model, so `WebApplicationFactory<Program>` runs it
through a deferred host builder: host configuration is merged *before*
`Program.cs`'s own config sources (`appsettings.json` included), so anything
added via `ConfigureHostConfiguration` gets silently overridden.
`ConfigureAppConfiguration` layers on top and actually wins. Every test class
that needs the host joins `[Collection(MongoCollection.Name)]` and takes
`MongoFixture` — one factory per test method is fine, one container is not.

## Running

Develop on the host with the .NET 10 SDK. Docker must be running, because
integration tests start their own MongoDB. They never touch the compose stack.

## Architecture guards

`ArchitectureTests` in `PSPad.TestInfrastructure`: Tasks purity (references
`Abstractions` only), Infrastructure references no module, the two allowed
module edges (`Statistics` → `Tasks`, `Sharing` → `Tasks`), Presentation
references `Abstractions` only.
