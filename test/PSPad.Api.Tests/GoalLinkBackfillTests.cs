using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class GoalLinkBackfillTests(MongoFixture fixture)
{
    [Fact]
    public async Task ATasksOwnGoalLinkMovesOntoTheGoalAndLeavesTheTask()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var user = Guid.NewGuid();
        var goal = await InsertGoal(context, user, ct);
        var task = await InsertTaskLinkedTo(context, user, goal, ct);
        var before = await LoadGoal(context, goal.Id, ct);

        await MongoBackfill.EnsureGoalLinksAsync(context, ct);

        var after = await LoadGoal(context, goal.Id, ct);
        Assert.Equal([task], after.TaskIds);
        Assert.True(after.Seq > before.Seq);
        Assert.Equal(before.Version, after.Version);
        Assert.False((await LoadRawTask(context, task, ct)).Contains("goalId"));
    }

    [Fact]
    public async Task ALinkToSomeoneElsesGoalIsDropped()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var goal = await InsertGoal(context, Guid.NewGuid(), ct);
        var task = await InsertTaskLinkedTo(context, Guid.NewGuid(), goal, ct);

        await MongoBackfill.EnsureGoalLinksAsync(context, ct);

        Assert.Empty((await LoadGoal(context, goal.Id, ct)).TaskIds);
        Assert.False((await LoadRawTask(context, task, ct)).Contains("goalId"));
    }

    [Fact]
    public async Task RunningItAgainChangesNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var user = Guid.NewGuid();
        var goal = await InsertGoal(context, user, ct);
        await InsertTaskLinkedTo(context, user, goal, ct);
        await MongoBackfill.EnsureGoalLinksAsync(context, ct);
        var once = await LoadGoal(context, goal.Id, ct);

        await MongoBackfill.EnsureGoalLinksAsync(context, ct);

        var twice = await LoadGoal(context, goal.Id, ct);
        Assert.Equal(once.TaskIds, twice.TaskIds);
        Assert.Equal(once.Seq, twice.Seq);
    }

    static async Task<Goal> InsertGoal(MongoContext context, Guid user, CancellationToken ct)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), user, Guid.NewGuid(), "Read more"), DateTimeOffset.UtcNow));
        await context.Collection<Goal>().InsertOneAsync(goal, cancellationToken: ct);
        return goal;
    }

    static async Task<Guid> InsertTaskLinkedTo(MongoContext context, Guid user, Goal goal, CancellationToken ct)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), Guid.NewGuid(), "Dune"), DateTimeOffset.UtcNow));
        var document = task.ToBsonDocument();
        document["goalId"] = goal.ToBsonDocument()["_id"];
        await context.Collection<BsonDocument>("todotasks").InsertOneAsync(document, cancellationToken: ct);
        return task.Id;
    }

    static Task<Goal> LoadGoal(MongoContext context, Guid goalId, CancellationToken ct) =>
        context.Collection<Goal>().Find(Builders<Goal>.Filter.Eq(goal => goal.Id, goalId)).SingleAsync(ct);

    static Task<BsonDocument> LoadRawTask(MongoContext context, Guid taskId, CancellationToken ct) =>
        context.Collection<BsonDocument>("todotasks")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(taskId, GuidRepresentation.Standard)))
            .SingleAsync(ct);
}
