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

**Unit** — pure, in-process, no Docker, no network, no filesystem. Domain rules,
the Today rule, recurrence derivation, client state folding, bUnit components.

**Integration** — real MongoDB via Testcontainers, real HTTP host. Command
handlers, transactions, idempotency, Today query, history, sync round trip, auth.

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test
```

Unit suite must stay under a few seconds. If it needs Docker, it is mislabeled.

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
