using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Commands;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class GoalLinkCommandTests(MongoFixture fixture)
{
    static async Task<Guid> ListAsync(HttpClient client, Guid user, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Books"));
        return listId;
    }

    static async Task<Guid[]> LinkedTasksAsync(HttpClient client, Guid goalId, CancellationToken ct)
    {
        var goal = Assert.Single(await SyncedAsync(client, "goals", ct), row => row.GetProperty("id").GetGuid() == goalId);
        return goal.GetProperty("_taskIds").EnumerateArray().Select(id => id.GetGuid()).ToArray();
    }

    [Fact]
    public async Task AddingATaskPutsItInTheGoal()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var goalId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateGoal(Guid.NewGuid(), user, goalId, "Read more"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Dune"));

        await SendAsync(client, ct, new AddTaskToGoal(Guid.NewGuid(), user, goalId, taskId));

        Assert.Equal([taskId], await LinkedTasksAsync(client, goalId, ct));
    }

    [Fact]
    public async Task AddingATaskAlreadyInTheGoalRecordsNothingMore()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var goalId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateGoal(Guid.NewGuid(), user, goalId, "Read more"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Dune"),
            new AddTaskToGoal(Guid.NewGuid(), user, goalId, taskId));

        await SendAsync(client, ct, new AddTaskToGoal(Guid.NewGuid(), user, goalId, taskId));

        Assert.Equal([taskId], await LinkedTasksAsync(client, goalId, ct));
        var added = await Persistence.TestContext.For(fixture).Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", goalId) &
                  Builders<BsonDocument>.Filter.Eq("type", nameof(TaskAddedToGoal)))
            .CountDocumentsAsync(ct);
        Assert.Equal(1, added);
    }

    [Fact]
    public async Task ATaskIsInAtMostOneOfAUsersGoals()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateGoal(Guid.NewGuid(), user, first, "Read more"),
            new CreateGoal(Guid.NewGuid(), user, second, "Learn Polish"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Dune"),
            new AddTaskToGoal(Guid.NewGuid(), user, first, taskId));

        await SendAsync(client, ct, new AddTaskToGoal(Guid.NewGuid(), user, second, taskId));

        Assert.Empty(await LinkedTasksAsync(client, first, ct));
        Assert.Equal([taskId], await LinkedTasksAsync(client, second, ct));
    }

    [Fact]
    public async Task ATaskInAListTheUserCannotSeeIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await SignInAsync(owner, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await SignInAsync(stranger, ct);
        var listId = await ListAsync(owner, ownerId, ct);
        var taskId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        await SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Private"));
        await SendAsync(stranger, ct, new CreateGoal(Guid.NewGuid(), strangerId, goalId, "Snooping"));

        var result = Assert.Single(await TrySendAsync(stranger, ct, new AddTaskToGoal(Guid.NewGuid(), strangerId, goalId, taskId)));

        Assert.False(result.Accepted);
        Assert.Empty(await LinkedTasksAsync(stranger, goalId, ct));
    }

    [Fact]
    public async Task RemovingTakesTheTaskOutAndRemovingAgainChangesNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var goalId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateGoal(Guid.NewGuid(), user, goalId, "Read more"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Dune"),
            new AddTaskToGoal(Guid.NewGuid(), user, goalId, taskId));

        await SendAsync(client, ct, new RemoveTaskFromGoal(Guid.NewGuid(), user, goalId, taskId));
        await SendAsync(client, ct, new RemoveTaskFromGoal(Guid.NewGuid(), user, goalId, taskId));

        Assert.Empty(await LinkedTasksAsync(client, goalId, ct));
        var removed = await Persistence.TestContext.For(fixture).Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", goalId) &
                  Builders<BsonDocument>.Filter.Eq("type", nameof(TaskRemovedFromGoal)))
            .CountDocumentsAsync(ct);
        Assert.Equal(1, removed);
    }
}
