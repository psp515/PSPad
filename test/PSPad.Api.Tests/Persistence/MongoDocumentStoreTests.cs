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
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var store = new MongoDocumentStore<Area>(context);
        var area = new Area();
        var events = Area.Decide(null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0),
            DateTimeOffset.UtcNow);
        area.ApplyAll(events);

        await context.Collection<Area>().InsertOneAsync(area, cancellationToken: ct);

        var loaded = await store.LoadAsync(area.Id, ct);

        Assert.NotNull(loaded);
        Assert.Equal("Home", loaded.Name);
        Assert.Equal(user, loaded.UserId);
        Assert.Equal(area.Version, loaded.Version);
    }

    [Fact]
    public async Task LoadingSomethingThatIsNotThereReturnsNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var store = new MongoDocumentStore<Area>(TestContext.For(fixture));

        Assert.Null(await store.LoadAsync(Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task LoadAllReturnsOnlyTheCallersDocuments()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var context = TestContext.For(fixture);
        await context.Collection<Area>().InsertOneAsync(AreaFor(mine, "Mine"), cancellationToken: ct);
        await context.Collection<Area>().InsertOneAsync(AreaFor(theirs, "Theirs"), cancellationToken: ct);

        var loaded = await new MongoDocumentStore<Area>(context).LoadAllAsync(mine, ct);

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
