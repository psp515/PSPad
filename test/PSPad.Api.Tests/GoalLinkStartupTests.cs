using System.Net.Http.Json;
using MongoDB.Bson;
using PSPad.Contracts;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class GoalLinkStartupTests(MongoFixture fixture)
{
    [Fact]
    public async Task ALegacyTaskGoalLinkReachesTheClientAfterTheApiStarts()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        Guid userId;
        await using (var before = new ApiFactory(fixture))
        {
            userId = (await before.ClientFor(subject).GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        }

        var context = Persistence.TestContext.For(fixture);
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), userId, Guid.NewGuid(), "Read more"), DateTimeOffset.UtcNow));
        goal.Seq = 1;
        await context.Collection<BsonDocument>("goals").InsertOneAsync(goal.ToBsonDocument(), cancellationToken: ct);
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), userId, Guid.NewGuid(), Guid.NewGuid(), "Dune"), DateTimeOffset.UtcNow));
        task.Seq = 2;
        var legacy = task.ToBsonDocument();
        legacy["goalId"] = goal.ToBsonDocument()["_id"];
        await context.Collection<BsonDocument>("todotasks").InsertOneAsync(legacy, cancellationToken: ct);

        await using var after = new ApiFactory(fixture);
        var client = after.ClientFor(subject);
        var sync = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!;

        var row = Assert.Single(sync.Documents["goals"], candidate => candidate.GetProperty("id").GetGuid() == goal.Id);
        Assert.Equal([task.Id], row.GetProperty("_taskIds").EnumerateArray().Select(id => id.GetGuid()));
    }
}
