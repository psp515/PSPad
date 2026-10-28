using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Commands;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ConflictRetryTests(MongoFixture fixture)
{
    [Fact]
    public async Task AConflictingSaveIsRetriedOnFreshDataUntilBothCommandsLand()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var reads = new StaleReads();
        await using var factory = FactoryWith(reads);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0));
        var beforeAny = await StoredAreaAsync(areaId, ct);
        await SendAsync(client, ct, new RenameArea(Guid.NewGuid(), user, areaId, "House"));
        reads.Serve(beforeAny, CommandDispatcher.MostAttempts - 1);

        var result = Assert.Single(await TrySendAsync(client, ct, new RenameArea(Guid.NewGuid(), user, areaId, "Flat")));

        Assert.True(result.Accepted, result.Rejection);
        var events = await Persistence.TestContext.For(fixture).Collection<StoredEvent>("events")
            .CountDocumentsAsync(entry => entry.AggregateId == areaId, cancellationToken: ct);
        Assert.Equal(3, events);
        var area = Assert.Single(await SyncedAsync(client, "areas", ct), row => row.GetProperty("id").GetGuid() == areaId);
        Assert.Equal("Flat", area.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ConflictsPastTheLastAttemptAreRejectedNotRetriedForever()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var reads = new StaleReads();
        await using var factory = FactoryWith(reads);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0));
        var beforeAny = await StoredAreaAsync(areaId, ct);
        await SendAsync(client, ct, new RenameArea(Guid.NewGuid(), user, areaId, "House"));
        reads.Serve(beforeAny, CommandDispatcher.MostAttempts);

        var result = Assert.Single(await TrySendAsync(client, ct, new RenameArea(Guid.NewGuid(), user, areaId, "Flat")));

        Assert.False(result.Accepted);
        Assert.Equal("Someone else changed this at the same time. Try again.", result.Rejection);
        var area = Assert.Single(await SyncedAsync(client, "areas", ct), row => row.GetProperty("id").GetGuid() == areaId);
        Assert.Equal("House", area.GetProperty("name").GetString());
    }

    ApiFactory FactoryWith(StaleReads reads) => new(fixture, testServices: services =>
        services.AddScoped<IDocumentStore<Area>>(provider => new StaleAreaStore(
            ActivatorUtilities.CreateInstance<MongoDocumentStore<Area>>(provider), reads)));

    async Task<Area> StoredAreaAsync(Guid areaId, CancellationToken ct) =>
        (await new MongoDocumentStore<Area>(Persistence.TestContext.For(fixture)).LoadAsync(areaId, ct))!;
}
