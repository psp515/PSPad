# API and Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the Tasks module behind MongoDB and HTTP: one transactional write
path, one command endpoint, one delta-sync endpoint, and a Today query — proven
against a real database.

**Architecture:** `MongoDocumentStore<T>` loads aggregates. `MongoUnitOfWork`
commits everything a command staged in one transaction, stamping each write with
a monotonic sequence and recording the command id so a replay is a no-op. A
dispatcher maps a wire envelope to a handler. The identity seam in this plan is
deliberately fake and plan 05 deletes it.

**Tech Stack:** .NET 10, MongoDB.Driver, ASP.NET Core Minimal API,
`System.Text.Json`, xUnit v3, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-12-slice-1-design.md`

## Global Constraints

- Target framework `net10.0`, `Nullable` enable, `TreatWarningsAsErrors` true.
- No comments in code, except one line for a genuinely counter-intuitive constraint saying *why*.
- Every test class carries `[UnitTest]` or `[IntegrationTest]`; integration classes also carry `[Collection(MongoCollection.Name)]`.
- `PSPad.Infrastructure` references no module assembly — it stores documents generically by `T`.
- Every write goes through one transaction that covers the aggregates, their events and the command id. A write that skips the event log is a bug.
- The sync marker is the sequence, never a clock.
- Tests isolate by `UserId`. Nothing truncates a collection between tests.
- Code, comments, commits and docs in English.

---

### Task 1: Mongo context and document store

**Files:**
- Create: `src/shared/PSPad.Infrastructure/Mongo/MongoOptions.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/MongoContext.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/MongoConventions.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/MongoDocumentStore.cs`
- Test: `test/PSPad.Api.Tests/Persistence/MongoDocumentStoreTests.cs`

**Interfaces:**
- Consumes: `Aggregate`, `IDocumentStore<T>` from plan 02.
- Produces: `MongoOptions { string ConnectionString; string Database }`, `MongoContext` with `IMongoClient Client`, `IMongoDatabase Database` and `IMongoCollection<T> Collection<T>()`, and `MongoDocumentStore<T> : IDocumentStore<T>`.

Collection names come from the aggregate type: `Area` → `areas`, `TaskList` →
`tasklists`, `TodoTask` → `todotasks`, `Goal` → `goals`, `Inbox` → `inboxes`,
`User` → `users`. Lower-cased type name plus `s`, with no special cases, so
`MongoContext` never needs a table of module types.

- [ ] **Step 1: Add the package**

```bash
dotnet add src/shared/PSPad.Infrastructure package MongoDB.Driver
```

- [ ] **Step 2: Write the failing test**

```csharp
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoDocumentStoreTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnAggregateRoundTripsThroughMongo()
    {
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var store = new MongoDocumentStore<Area>(context);
        var area = new Area();
        var events = Area.Decide(null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0),
            DateTimeOffset.UtcNow);
        area.ApplyAll(events);

        await context.Collection<Area>().InsertOneAsync(area);

        var loaded = await store.LoadAsync(area.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Home", loaded.Name);
        Assert.Equal(user, loaded.UserId);
        Assert.Equal(area.Version, loaded.Version);
    }

    [Fact]
    public async Task LoadingSomethingThatIsNotThereReturnsNothing()
    {
        var store = new MongoDocumentStore<Area>(TestContext.For(fixture));

        Assert.Null(await store.LoadAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task LoadAllReturnsOnlyTheCallersDocuments()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var context = TestContext.For(fixture);
        await context.Collection<Area>().InsertOneAsync(AreaFor(mine, "Mine"));
        await context.Collection<Area>().InsertOneAsync(AreaFor(theirs, "Theirs"));

        var loaded = await new MongoDocumentStore<Area>(context).LoadAllAsync(mine, CancellationToken.None);

        Assert.Equal(["Mine"], loaded.Select(area => area.Name));
    }

    static Area AreaFor(Guid user, string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), name, 0),
            DateTimeOffset.UtcNow));
        return area;
    }
}
```

Add the shared helper `test/PSPad.Api.Tests/Persistence/TestContext.cs`:

```csharp
using Microsoft.Extensions.Options;
using PSPad.Infrastructure.Mongo;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

public static class TestContext
{
    public static MongoContext For(MongoFixture fixture)
    {
        MongoConventions.Register();
        return new MongoContext(Options.Create(new MongoOptions
        {
            ConnectionString = fixture.ConnectionString,
            Database = "pspad_test"
        }));
    }
}
```

`TestContext` has no test methods, so the category guard leaves it alone.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: compile error — `MongoContext` does not exist.

- [ ] **Step 4: Write the options and context**

```csharp
namespace PSPad.Infrastructure.Mongo;

public sealed class MongoOptions
{
    public const string Section = "Mongo";

    public string ConnectionString { get; init; } = "";

    public string Database { get; init; } = "pspad";
}
```

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public sealed class MongoContext
{
    public MongoContext(IOptions<MongoOptions> options)
    {
        Client = new MongoClient(options.Value.ConnectionString);
        Database = Client.GetDatabase(options.Value.Database);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<T> Collection<T>() where T : Aggregate =>
        Database.GetCollection<T>(NameOf(typeof(T)));

    public IMongoCollection<TDocument> Collection<TDocument>(string name) =>
        Database.GetCollection<TDocument>(name);

    public static string NameOf(Type aggregate) => aggregate.Name.ToLowerInvariant() + "s";
}
```

`Microsoft.Extensions.Options` comes with the driver's dependencies; if the
project does not resolve it, add `Microsoft.Extensions.Options` explicitly.

- [ ] **Step 5: Write the conventions**

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public static class MongoConventions
{
    static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new DateOnlySerializer());

        ConventionRegistry.Register(
            "pspad",
            new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new MapIdConvention()
            },
            type => typeof(Aggregate).IsAssignableFrom(type) || type.Namespace?.StartsWith("PSPad") == true);
    }

    sealed class MapIdConvention : ConventionBase, IClassMapConvention
    {
        public void Apply(BsonClassMap classMap)
        {
            var id = classMap.AllMemberMaps.FirstOrDefault(member => member.MemberName == "Id");
            if (id is not null)
            {
                classMap.SetIdMember(id);
            }
        }
    }
}
```

Aggregates expose `private set` properties, so also enable non-public member
access by registering a `BsonClassMap` that maps them; the simplest route is to
add to the convention pack:

```csharp
                new MemberSerializationOptionsConvention(typeof(Guid), new { }),
```

If that proves awkward, map the writable surface explicitly per aggregate in
`MongoConventions` using `BsonClassMap.RegisterClassMap<T>(map => map.AutoMap())`
plus `map.MapProperty(...).SetIsRequired(true)`. The test in Step 2 asserting
`Name`, `UserId` and `Version` survive the round trip is what tells you the
mapping is right — do not move on while it is red.

`DateOnlySerializer` ships with the driver from 2.x; if the installed version
lacks it, write one that stores `yyyy-MM-dd` as a string and register it here.

- [ ] **Step 6: Write the document store**

```csharp
using MongoDB.Driver;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public sealed class MongoDocumentStore<T>(MongoContext context) : IDocumentStore<T> where T : Aggregate
{
    public async Task<T?> LoadAsync(Guid id, CancellationToken ct) =>
        await context.Collection<T>()
            .Find(Builders<T>.Filter.Eq(document => document.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct) =>
        await context.Collection<T>()
            .Find(Builders<T>.Filter.Eq(document => document.UserId, userId))
            .ToListAsync(ct);
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: PASS, three tests.

- [ ] **Step 8: Commit**

```bash
git add src test
git commit -m "feat: store aggregates as Mongo documents"
```

---

### Task 2: Transactional unit of work

**Files:**
- Create: `src/shared/PSPad.Infrastructure/Mongo/StoredEvent.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/ProcessedCommand.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/SequenceSource.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/MongoUnitOfWork.cs`
- Test: `test/PSPad.Api.Tests/Persistence/MongoUnitOfWorkTests.cs`

**Interfaces:**
- Consumes: `IUnitOfWork`, `Aggregate`, `DomainEvent` from plan 02; `MongoContext` from Task 1.
- Produces: `StoredEvent(long Seq, Guid UserId, string AggregateType, Guid AggregateId, string Type, string Payload, DateTimeOffset At)` in collection `events`; `ProcessedCommand(Guid Id, Guid UserId, DateTimeOffset At)` in `processed_commands`; `SequenceSource.NextAsync(IClientSessionHandle, CancellationToken)`; `MongoUnitOfWork : IUnitOfWork`, registered scoped so each request gets its own staging list.

Aggregates carry `Seq` for delta sync, so add to `Aggregate` in
`PSPad.Abstractions`:

```csharp
    public long Seq { get; set; }
```

That one settable property is the only concession the pure module makes to
persistence. It is data about the write, never about the domain, and nothing in
`Decide` may read it.

- [ ] **Step 1: Write the failing test**

```csharp
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoUnitOfWorkTests(MongoFixture fixture)
{
    [Fact]
    public async Task CommittingWritesTheAggregateTheEventAndTheCommandId()
    {
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var work = new MongoUnitOfWork(context);
        var commandId = Guid.NewGuid();
        var (area, events) = NewArea(user);

        work.Stage(area, events);
        await work.CommitAsync(commandId, user, CancellationToken.None);

        var stored = await new MongoDocumentStore<Area>(context).LoadAsync(area.Id, CancellationToken.None);
        var log = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user)).ToListAsync();
        var processed = await context.Collection<ProcessedCommand>("processed_commands")
            .Find(Builders<ProcessedCommand>.Filter.Eq(entry => entry.Id, commandId)).ToListAsync();

        Assert.NotNull(stored);
        Assert.Equal(nameof(AreaCreated), Assert.Single(log).Type);
        Assert.Single(processed);
    }

    [Fact]
    public async Task TheSequenceAdvancesAndStampsBothTheEventAndTheAggregate()
    {
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var (first, firstEvents) = NewArea(user);
        var (second, secondEvents) = NewArea(user);

        var work = new MongoUnitOfWork(context);
        work.Stage(first, firstEvents);
        await work.CommitAsync(Guid.NewGuid(), user, CancellationToken.None);

        var later = new MongoUnitOfWork(context);
        later.Stage(second, secondEvents);
        await later.CommitAsync(Guid.NewGuid(), user, CancellationToken.None);

        var store = new MongoDocumentStore<Area>(context);
        var storedFirst = await store.LoadAsync(first.Id, CancellationToken.None);
        var storedSecond = await store.LoadAsync(second.Id, CancellationToken.None);

        Assert.True(storedSecond!.Seq > storedFirst!.Seq);
    }

    [Fact]
    public async Task ReplayingACommandIdWritesNothingASecondTime()
    {
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var commandId = Guid.NewGuid();
        var (area, events) = NewArea(user);

        var first = new MongoUnitOfWork(context);
        first.Stage(area, events);
        await first.CommitAsync(commandId, user, CancellationToken.None);

        var replay = new MongoUnitOfWork(context);
        replay.Stage(area, events);
        await replay.CommitAsync(commandId, user, CancellationToken.None);

        var log = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user)).ToListAsync();

        Assert.Single(log);
    }

    static (Area Area, IReadOnlyList<DomainEvent> Events) NewArea(Guid user)
    {
        var area = new Area();
        var events = Area.Decide(
            null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0), DateTimeOffset.UtcNow);
        area.ApplyAll(events);
        return (area, events);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: compile error — `MongoUnitOfWork` does not exist.

- [ ] **Step 3: Write the stored shapes**

```csharp
namespace PSPad.Infrastructure.Mongo;

public sealed class StoredEvent
{
    public long Seq { get; init; }
    public Guid UserId { get; init; }
    public string AggregateType { get; init; } = "";
    public Guid AggregateId { get; init; }
    public string Type { get; init; } = "";
    public string Payload { get; init; } = "";
    public DateTimeOffset At { get; init; }
}
```

```csharp
namespace PSPad.Infrastructure.Mongo;

public sealed class ProcessedCommand
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset At { get; init; }
}
```

- [ ] **Step 4: Write the sequence source**

```csharp
using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public sealed class SequenceSource(MongoContext context)
{
    public async Task<long> NextAsync(IClientSessionHandle session, CancellationToken ct)
    {
        var counters = context.Collection<BsonDocument>("counters");
        var updated = await counters.FindOneAndUpdateAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", "events"),
            Builders<BsonDocument>.Update.Inc("value", 1L),
            new FindOneAndUpdateOptions<BsonDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            ct);

        return updated["value"].ToInt64();
    }
}
```

- [ ] **Step 5: Write the unit of work**

```csharp
using System.Text.Json;
using MongoDB.Driver;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public sealed class MongoUnitOfWork(MongoContext context) : IUnitOfWork
{
    readonly List<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> _staged = [];
    readonly SequenceSource _sequence = new(context);

    public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) =>
        _staged.Add((aggregate, events));

    public async Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct)
    {
        if (_staged.Count == 0)
        {
            return;
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            var processed = context.Collection<ProcessedCommand>("processed_commands");
            var already = await processed
                .Find(session, Builders<ProcessedCommand>.Filter.Eq(entry => entry.Id, commandId))
                .AnyAsync(ct);

            if (already)
            {
                await session.AbortTransactionAsync(ct);
                return;
            }

            var events = context.Collection<StoredEvent>("events");

            foreach (var (aggregate, staged) in _staged)
            {
                foreach (var @event in staged)
                {
                    var seq = await _sequence.NextAsync(session, ct);
                    aggregate.Seq = seq;
                    await events.InsertOneAsync(session, new StoredEvent
                    {
                        Seq = seq,
                        UserId = @event.UserId,
                        AggregateType = aggregate.GetType().Name,
                        AggregateId = @event.AggregateId,
                        Type = @event.GetType().Name,
                        Payload = JsonSerializer.Serialize(@event, @event.GetType()),
                        At = @event.At
                    }, cancellationToken: ct);
                }

                await ReplaceAsync(session, aggregate, ct);
            }

            await processed.InsertOneAsync(session, new ProcessedCommand
            {
                Id = commandId,
                UserId = userId,
                At = DateTimeOffset.UtcNow
            }, cancellationToken: ct);

            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
        finally
        {
            _staged.Clear();
        }
    }

    Task ReplaceAsync(IClientSessionHandle session, Aggregate aggregate, CancellationToken ct)
    {
        var collection = context.Database.GetCollection<Aggregate>(MongoContext.NameOf(aggregate.GetType()));
        return collection.ReplaceOneAsync(
            session,
            Builders<Aggregate>.Filter.Eq(document => document.Id, aggregate.Id),
            aggregate,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }
}
```

`ReplaceOneAsync` over `IMongoCollection<Aggregate>` serializes the runtime type
only if the class map for that type is registered — `MongoConventions.Register()`
must have run before the first write, which `AddPSPadInfrastructure` in Task 3
guarantees. If a document comes back missing its module-specific fields, that
registration is what to look at first.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: PASS, three tests.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: commit aggregates, events and the command id together"
```

---

### Task 3: Command envelope and dispatch

**Files:**
- Create: `src/shared/PSPad.Contracts/CommandEnvelope.cs`
- Create: `src/shared/PSPad.Contracts/CommandResponse.cs`
- Create: `src/shared/PSPad.Contracts/CommandCatalogue.cs`
- Create: `src/PSPad.Api/Commands/CommandDispatcher.cs`
- Create: `src/PSPad.Api/Commands/CommandRegistration.cs`
- Create: `src/shared/PSPad.Infrastructure/InfrastructureRegistration.cs`
- Test: `test/PSPad.Api.Tests/Commands/CommandDispatcherTests.cs`

**Interfaces:**
- Consumes: `ICommandHandler<T>` from plan 02; every command type in `PSPad.Module.Tasks`.
- Produces: `CommandEnvelope(string Type, JsonElement Payload)`, `CommandResponse(Guid CommandId, bool Accepted, string? Rejection)`, `CommandCatalogue.Resolve(string type)` mapping a wire name to a `Type`, `CommandDispatcher.DispatchAsync(CommandEnvelope, Guid userId, CancellationToken)`, `services.AddPSPadInfrastructure(configuration)` and `services.AddPSPadCommands()`.

The wire name of a command is its type name — `CreateArea`, `AddStep`,
`CompleteOccurrence`. The catalogue builds its map by reflecting over
`PSPad.Module.Tasks` once at startup, so adding a command needs no registration
step and no second list to forget.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[UnitTest]
public class CommandCatalogueTests
{
    [Fact]
    public void ACommandResolvesByItsTypeName()
    {
        Assert.Equal(typeof(CreateArea), CommandCatalogue.Resolve("CreateArea"));
    }

    [Fact]
    public void AnUnknownNameResolvesToNothing()
    {
        Assert.Null(CommandCatalogue.Resolve("DropDatabase"));
    }
}
```

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[UnitTest]
public class CommandDispatcherTests
{
    [Fact]
    public async Task AnEnvelopeReachesTheRightHandler()
    {
        var work = new FakeUnitOfWork();
        var services = new ServiceCollection()
            .AddSingleton<IUnitOfWork>(work)
            .AddSingleton<IClock>(new FixedClock(DateTimeOffset.UnixEpoch))
            .AddSingleton<IDocumentStore<Area>>(new FakeDocumentStore<Area>())
            .AddPSPadCommands()
            .BuildServiceProvider();
        var user = Guid.NewGuid();
        var command = new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0);
        var envelope = new CommandEnvelope(
            nameof(CreateArea), JsonSerializer.SerializeToElement(command));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, user, CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.Single(work.Events);
    }

    [Fact]
    public async Task AnEnvelopeForSomebodyElsesUserIdIsRejected()
    {
        var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();
        var envelope = new CommandEnvelope(
            nameof(CreateArea),
            JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0)));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, Guid.NewGuid(), CancellationToken.None);

        Assert.False(response.Accepted);
    }

    [Fact]
    public async Task AnUnknownCommandTypeIsRejectedRatherThanThrown()
    {
        var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();

        var response = await new CommandDispatcher(services).DispatchAsync(
            new CommandEnvelope("DropDatabase", JsonSerializer.SerializeToElement(new { })),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(response.Accepted);
    }
}
```

The dispatcher test needs the module's test fakes, so reference them:

```bash
dotnet add test/PSPad.Api.Tests reference test/PSPad.Module.Tasks.Tests
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Unit`
Expected: compile error — `CommandEnvelope` does not exist.

- [ ] **Step 3: Write the contracts**

```csharp
using System.Text.Json;

namespace PSPad.Contracts;

public sealed record CommandEnvelope(string Type, JsonElement Payload);
```

```csharp
namespace PSPad.Contracts;

public sealed record CommandResponse(Guid CommandId, bool Accepted, string? Rejection);
```

```csharp
using System.Reflection;
using PSPad.Abstractions;

namespace PSPad.Contracts;

public static class CommandCatalogue
{
    static readonly Dictionary<string, Type> Commands = Build();

    public static Type? Resolve(string type) => Commands.GetValueOrDefault(type);

    public static IReadOnlyCollection<Type> All => Commands.Values;

    static Dictionary<string, Type> Build()
    {
        var module = Assembly.Load("PSPad.Module.Tasks");
        return module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } &&
                typeof(ICommand).IsAssignableFrom(type))
            .ToDictionary(type => type.Name, type => type);
    }
}
```

`PSPad.Contracts` loads the module by name rather than referencing it, because a
reference the other way would put the module below the wire contracts and make
the dependency table in the spec a lie. `PSPad.Api` references both, so the
assembly is always present at runtime; the unit test above fails loudly if it
ever is not.

- [ ] **Step 4: Write the registration and dispatcher**

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;

namespace PSPad.Api.Commands;

public static class CommandRegistration
{
    public static IServiceCollection AddPSPadCommands(this IServiceCollection services)
    {
        var module = Assembly.Load("PSPad.Module.Tasks");

        foreach (var handler in module.GetTypes().Where(type => type is { IsAbstract: false, IsClass: true }))
        {
            foreach (var contract in handler.GetInterfaces()
                .Where(@interface => @interface.IsGenericType &&
                    @interface.GetGenericTypeDefinition() == typeof(ICommandHandler<>)))
            {
                services.AddScoped(contract, handler);
            }
        }

        return services;
    }
}
```

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Contracts;

namespace PSPad.Api.Commands;

public sealed class CommandDispatcher(IServiceProvider services)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CommandResponse> DispatchAsync(
        CommandEnvelope envelope, Guid userId, CancellationToken ct)
    {
        var type = CommandCatalogue.Resolve(envelope.Type);
        if (type is null)
        {
            return new CommandResponse(Guid.Empty, false, $"Unknown command {envelope.Type}.");
        }

        if (envelope.Payload.Deserialize(type, Json) is not ICommand command)
        {
            return new CommandResponse(Guid.Empty, false, $"Malformed payload for {envelope.Type}.");
        }

        if (command.UserId != userId)
        {
            return new CommandResponse(command.CommandId, false, "That command is for a different user.");
        }

        var handler = services.GetService(typeof(ICommandHandler<>).MakeGenericType(type));
        if (handler is null)
        {
            return new CommandResponse(command.CommandId, false, $"No handler for {envelope.Type}.");
        }

        var method = handler.GetType().GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
        var result = await (Task<CommandResult>)method.Invoke(handler, [command, ct])!;

        return new CommandResponse(command.CommandId, result.Accepted, result.Rejection);
    }
}
```

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddPSPadInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        MongoConventions.Register();

        services.Configure<MongoOptions>(options =>
        {
            options = options with { };
        });
        services.AddOptions<MongoOptions>()
            .Configure(options => configuration.GetSection(MongoOptions.Section).Bind(options))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    throw new InvalidOperationException("ConnectionStrings:Mongo is not configured.");
                }
            });

        services.AddSingleton<MongoContext>();
        services.AddScoped(typeof(IDocumentStore<>), typeof(MongoDocumentStore<>));
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
```

`MongoOptions` is a class with `init` setters, so drop the `with` line above and
bind straight into it:

```csharp
        services.AddOptions<MongoOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection(MongoOptions.Section).Bind(options);
            });
```

Add `src/shared/PSPad.Infrastructure/SystemClock.cs`:

```csharp
using PSPad.Abstractions;

namespace PSPad.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Unit`
Expected: PASS, five tests across the two classes.

- [ ] **Step 6: Commit**

```bash
git add src test
git commit -m "feat: dispatch wire envelopes to module handlers"
```

---

### Task 4: The commands endpoint

**Files:**
- Modify: `src/PSPad.Api/Program.cs`
- Create: `src/PSPad.Api/Identity/CurrentUser.cs`
- Create: `src/PSPad.Api/Endpoints/CommandEndpoints.cs`
- Create: `src/PSPad.Api/appsettings.json`
- Test: `test/PSPad.Api.Tests/Endpoints/CommandEndpointTests.cs`
- Test: `test/PSPad.Api.Tests/ApiFactory.cs`

**Interfaces:**
- Consumes: `CommandDispatcher` from Task 3.
- Produces: `POST /api/commands` taking `CommandEnvelope[]` and returning `CommandResponse[]`; `ICurrentUser { Guid UserId }` with the header-reading implementation `HeaderCurrentUser`; `ApiFactory` for tests.

`HeaderCurrentUser` reads `X-User-Id`. It exists so this plan can be tested
before Keycloak, and **plan 05 deletes it**. Do not build anything else on it and
do not expose this API to a network until that has happened.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommandEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnAcceptedCommandComesBackAcceptedAndIsStored()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        var command = new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0);

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command))
        });

        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>();
        Assert.True(Assert.Single(results!).Accepted);
    }

    [Fact]
    public async Task ABatchIsAppliedInOrderAndReportsEachResult()
    {
        var user = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new RenameArea(Guid.NewGuid(), user, areaId, "House")),
            Envelope(new RenameArea(Guid.NewGuid(), user, Guid.NewGuid(), "Nowhere"))
        });

        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>();

        Assert.True(results![0].Accepted);
        Assert.True(results[1].Accepted);
        Assert.False(results[2].Accepted);
        Assert.NotNull(results[2].Rejection);
    }

    [Fact]
    public async Task ReplayingTheSameBatchChangesNothing()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        var batch = new[] { Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)) };

        await client.PostAsJsonAsync("/api/commands", batch);
        var replay = await client.PostAsJsonAsync("/api/commands", batch);

        var results = await replay.Content.ReadFromJsonAsync<CommandResponse[]>();
        Assert.True(Assert.Single(results!).Accepted);

        var sync = await client.GetFromJsonAsync<JsonElement>("/api/sync?since=0");
        Assert.Equal(1, sync.GetProperty("events").GetArrayLength());
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
```

The third test reaches `/api/sync`, which Task 5 adds. Write it now and expect it
red until then — it is the test that proves idempotency end to end, and splitting
it across two tasks would lose that.

`test/PSPad.Api.Tests/ApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

public sealed class ApiFactory(MongoFixture fixture) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = "pspad_test"
            }));

        return base.CreateHost(builder);
    }

    public HttpClient ClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: FAIL — `/api/commands` returns 404.

- [ ] **Step 3: Write the identity seam**

```csharp
namespace PSPad.Api.Identity;

public interface ICurrentUser
{
    Guid UserId { get; }
}

public sealed class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers["X-User-Id"], out var id)
            ? id
            : throw new InvalidOperationException("X-User-Id is missing.");
}
```

- [ ] **Step 4: Write the endpoint**

```csharp
using PSPad.Api.Commands;
using PSPad.Api.Identity;
using PSPad.Contracts;

namespace PSPad.Api.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/commands", async (
            CommandEnvelope[] envelopes,
            CommandDispatcher dispatcher,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var responses = new List<CommandResponse>(envelopes.Length);

            foreach (var envelope in envelopes)
            {
                responses.Add(await dispatcher.DispatchAsync(envelope, user.UserId, ct));
            }

            return Results.Ok(responses);
        });
    }
}
```

The loop is sequential on purpose. The outbox ships commands in order, and a task
created by one envelope has to exist before the next envelope renames it.

- [ ] **Step 5: Wire up Program**

```csharp
using PSPad.Api.Commands;
using PSPad.Api.Endpoints;
using PSPad.Api.Identity;
using PSPad.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPSPadInfrastructure(builder.Configuration);
builder.Services.AddPSPadCommands();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();
builder.Services.AddScoped<CommandDispatcher>();

var app = builder.Build();

app.MapGet("/health", () => "healthy");
app.MapCommandEndpoints();

app.Run();

public partial class Program;
```

`src/PSPad.Api/appsettings.json`:

```json
{
  "Mongo": {
    "ConnectionString": "mongodb://pspad:pspad@localhost:27017/pspad?authSource=admin&replicaSet=rs0",
    "Database": "pspad"
  }
}
```

- [ ] **Step 6: Run test to verify the first two pass**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: the first two tests PASS; `ReplayingTheSameBatchChangesNothing` still
fails on the missing `/api/sync`.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: accept batches of commands over HTTP"
```

---

### Task 5: Delta sync

**Files:**
- Create: `src/shared/PSPad.Contracts/SyncResponse.cs`
- Create: `src/PSPad.Api/Endpoints/SyncEndpoints.cs`
- Create: `src/PSPad.Api/Sync/SyncReader.cs`
- Modify: `src/PSPad.Api/Program.cs`
- Test: `test/PSPad.Api.Tests/Endpoints/SyncEndpointTests.cs`

**Interfaces:**
- Consumes: `MongoContext`, `StoredEvent` from Task 2.
- Produces: `SyncResponse(long Marker, IReadOnlyDictionary<string, JsonElement[]> Documents, SyncEvent[] Events)` where `SyncEvent(long Seq, string AggregateType, Guid AggregateId, string Type, string Payload, DateTimeOffset At)`, and `GET /api/sync?since={seq}`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SyncEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task SyncingFromZeroReturnsEverythingTheUserHas()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)))
        });

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0");

        Assert.True(sync!.Marker > 0);
        Assert.Single(sync.Documents["areas"]);
        Assert.Single(sync.Events);
    }

    [Fact]
    public async Task SyncingFromTheLastMarkerReturnsOnlyWhatChangedSince()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)))
        });
        var first = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0");

        var second = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}");

        Assert.Empty(second!.Events);
        Assert.Equal(first.Marker, second.Marker);
    }

    [Fact]
    public async Task SyncNeverLeaksAnotherUsersDocuments()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        await factory.ClientFor(theirs).PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), theirs, Guid.NewGuid(), "Theirs", 0)))
        });

        var sync = await factory.ClientFor(mine).GetFromJsonAsync<SyncResponse>("/api/sync?since=0");

        Assert.Empty(sync!.Events);
        Assert.False(sync.Documents.TryGetValue("areas", out var areas) && areas.Length > 0);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: FAIL — `/api/sync` returns 404.

- [ ] **Step 3: Write the contract**

```csharp
using System.Text.Json;

namespace PSPad.Contracts;

public sealed record SyncEvent(
    long Seq, string AggregateType, Guid AggregateId, string Type, string Payload, DateTimeOffset At);

public sealed record SyncResponse(
    long Marker,
    IReadOnlyDictionary<string, JsonElement[]> Documents,
    SyncEvent[] Events);
```

- [ ] **Step 4: Write the reader**

```csharp
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Sync;

public sealed class SyncReader(MongoContext context)
{
    static readonly string[] Collections = ["areas", "tasklists", "todotasks", "goals", "inboxes", "users"];

    public async Task<SyncResponse> ReadAsync(Guid userId, long since, CancellationToken ct)
    {
        var events = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId) &
                  Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, since))
            .SortBy(entry => entry.Seq)
            .ToListAsync(ct);

        var documents = new Dictionary<string, JsonElement[]>();

        foreach (var name in Collections)
        {
            var rows = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", new BsonBinaryData(userId, GuidRepresentation.Standard)) &
                      Builders<BsonDocument>.Filter.Gt("seq", since))
                .ToListAsync(ct);

            documents[name] = rows
                .Select(row => JsonSerializer.Deserialize<JsonElement>(row.ToJson()))
                .ToArray();
        }

        var marker = events.Count > 0
            ? events[^1].Seq
            : Math.Max(since, await HighestSeqAsync(userId, ct));

        return new SyncResponse(
            marker,
            documents,
            events.Select(entry => new SyncEvent(
                entry.Seq, entry.AggregateType, entry.AggregateId, entry.Type, entry.Payload, entry.At))
                .ToArray());
    }

    async Task<long> HighestSeqAsync(Guid userId, CancellationToken ct)
    {
        var newest = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId))
            .SortByDescending(entry => entry.Seq)
            .FirstOrDefaultAsync(ct);

        return newest?.Seq ?? 0;
    }
}
```

The marker comes from the newest event the *caller* can see, not from the global
counter. A marker taken from the counter would jump past another user's writes
and the client would never ask for the range again.

- [ ] **Step 5: Write the endpoint and register the reader**

```csharp
using PSPad.Api.Identity;
using PSPad.Api.Sync;

namespace PSPad.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sync", async (
            long since, SyncReader reader, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(await reader.ReadAsync(user.UserId, since, ct)));
    }
}
```

In `Program.cs` add `builder.Services.AddScoped<SyncReader>();` and
`app.MapSyncEndpoints();`.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: PASS, including `ReplayingTheSameBatchChangesNothing` from Task 4.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: pull changes by sequence marker"
```

---

### Task 6: Today query and indexes

**Files:**
- Create: `src/PSPad.Api/Endpoints/TodayEndpoints.cs`
- Create: `src/shared/PSPad.Infrastructure/Mongo/MongoIndexes.cs`
- Modify: `src/PSPad.Api/Program.cs`
- Test: `test/PSPad.Api.Tests/Endpoints/TodayEndpointTests.cs`

**Interfaces:**
- Consumes: `TodayRule` and `Occurrences` from plan 03, `IDocumentStore<TodoTask>` from Task 1.
- Produces: `GET /api/today` returning `TodayEntry[]`, and `MongoIndexes.EnsureAsync(MongoContext, CancellationToken)` run once at startup.

Until plan 05 there is no `User` document to read a time zone from, so this
endpoint takes the zone from the `X-Time-Zone` header and defaults to `Etc/UTC`.
Plan 05 replaces that with `users.timeZone` and deletes the header.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class TodayEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task ATaskDueYesterdayComesBackOverdue()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        var taskId = await SeedTask(client, user);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskDueDate(Guid.NewGuid(), user, taskId, yesterday))
        });

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today");

        Assert.True(Assert.Single(today!).Overdue);
    }

    [Fact]
    public async Task ARecurringTaskMissedYesterdayIsNeverOverdue()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        var taskId = await SeedTask(client, user);
        var lastWeek = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-7);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskRecurrence(
                Guid.NewGuid(), user, taskId, RecurrenceRule.Daily(lastWeek)))
        });

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today");

        var entry = Assert.Single(today!);
        Assert.False(entry.Overdue);
        Assert.True(entry.Recurring);
    }

    [Fact]
    public async Task TodayIsReadInTheZoneTheCallerReports()
    {
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        client.DefaultRequestHeaders.Add("X-Time-Zone", "Pacific/Auckland");
        var taskId = await SeedTask(client, user);
        var aucklandToday = TodayRule.TodayIn(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"));
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskDueDate(Guid.NewGuid(), user, taskId, aucklandToday))
        });

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today");

        Assert.False(Assert.Single(today!).Overdue);
    }

    static async Task<Guid> SeedTask(HttpClient client, Guid user)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new PSPad.Module.Tasks.Areas.CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands", 0)),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"))
        });

        return taskId;
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: FAIL — `/api/today` returns 404.

- [ ] **Step 3: Write the endpoint**

```csharp
using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Api.Endpoints;

public static class TodayEndpoints
{
    public static void MapTodayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/today", async (
            HttpRequest request,
            IDocumentStore<TodoTask> tasks,
            ICurrentUser user,
            IClock clock,
            CancellationToken ct) =>
        {
            var zone = ZoneFrom(request.Headers["X-Time-Zone"]);
            var today = TodayRule.TodayIn(clock.UtcNow, zone);
            var all = await tasks.LoadAllAsync(user.UserId, ct);

            return Results.Ok(TodayRule.Select(all, today));
        });
    }

    static TimeZoneInfo ZoneFrom(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
```

In `Program.cs` add `app.MapTodayEndpoints();`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: PASS, three tests.

- [ ] **Step 5: Write the indexes**

```csharp
using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public static class MongoIndexes
{
    static readonly string[] AggregateCollections =
        ["areas", "tasklists", "todotasks", "goals", "inboxes", "users"];

    public static async Task EnsureAsync(MongoContext context, CancellationToken ct)
    {
        foreach (var name in AggregateCollections)
        {
            await context.Collection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("seq")),
                cancellationToken: ct);
        }

        await context.Collection<BsonDocument>("todotasks").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("listId")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("dueOn"))
        ], ct);

        await context.Collection<BsonDocument>("tasklists").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("areaId")),
            cancellationToken: ct);

        await context.Collection<BsonDocument>("events").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("seq")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("at"))
        ], ct);

        await context.Collection<BsonDocument>("processed_commands").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("at"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) }),
            cancellationToken: ct);
    }
}
```

In `Program.cs`, before `app.Run()`:

```csharp
await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);
```

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test`
Expected: PASS, unit and integration together.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: answer Today from the server and index for sync"
```

---

## Done when

`dotnet test` passes; a command posted twice produces one event; `/api/sync`
returns only the caller's documents and a marker that does not move when nothing
changed; and a daily recurring task missed yesterday comes back from `/api/today`
not overdue.
