using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using MongoDB.Bson;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
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

    [Fact]
    public async Task AGoalsStatusAndDueDateRoundTripThroughMongo()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), user, Guid.NewGuid(), "Run"), DateTimeOffset.UtcNow));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), user, goal.Id, GoalStatus.NotAchieved),
            DateTimeOffset.UtcNow));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), user, goal.Id, new DateOnly(2026, 12, 31)),
            DateTimeOffset.UtcNow));

        await context.Collection<Goal>().InsertOneAsync(goal, cancellationToken: ct);
        var loaded = await new MongoDocumentStore<Goal>(context).LoadAsync(goal.Id, ct);

        Assert.Equal(GoalStatus.NotAchieved, loaded!.Status);
        Assert.Equal(new DateOnly(2026, 12, 31), loaded.DueOn);
    }

    [Fact]
    public async Task AGoalStoredBeforeStatusesExistedReadsAsAchieved()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var context = TestContext.For(fixture);
        await context.Collection<BsonDocument>(context.Collection<Goal>().CollectionNamespace.CollectionName)
            .InsertOneAsync(new BsonDocument
            {
                ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
                ["userId"] = new BsonBinaryData(user, GuidRepresentation.Standard),
                ["name"] = "Run",
                ["achieved"] = true,
                ["version"] = 2,
                ["seq"] = 1L,
                ["deleted"] = false
            }, cancellationToken: ct);

        var loaded = await new MongoDocumentStore<Goal>(context).LoadAsync(id, ct);

        Assert.Equal(GoalStatus.Achieved, loaded!.Status);
        Assert.Null(loaded.DueOn);
    }

    static Area AreaFor(Guid user, string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), name, 0),
            DateTimeOffset.UtcNow));
        return area;
    }
}
