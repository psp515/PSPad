using System.Net.Http.Json;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SharedStatisticsTests(MongoFixture fixture)
{
    [Fact]
    public async Task AMemberCompletingTheOwnersTaskShowsOnBothFeedsButNotTheMembersByGoal()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var goalId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateGoal(Guid.NewGuid(), ownerId, goalId, "Read more"));
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));
        await Sharing.SendAsync(owner, ct, new LinkTaskToGoal(Guid.NewGuid(), ownerId, taskId, goalId));

        await Sharing.SendAsync(member, ct, new CompleteTask(Guid.NewGuid(), memberId, taskId));

        var ownerRecords = await EventuallyAsync(
            () => Records(owner, ct),
            feed => feed.Any(record => record.Kind == "Completed" && record.TaskId == taskId),
            ct);
        var memberRecords = await EventuallyAsync(
            () => Records(member, ct),
            feed => feed.Any(record => record.Kind == "Completed" && record.TaskId == taskId),
            ct);

        Assert.Contains(ownerRecords, record => record.Kind == "Completed" && record.TaskId == taskId);
        Assert.Contains(memberRecords, record => record.Kind == "Completed" && record.TaskId == taskId);

        var memberOverview = await Overview(member, ct);
        Assert.DoesNotContain(memberOverview.ByGoal, bar => bar.GoalId == goalId);

        var ownerOverview = await Overview(owner, ct);
        Assert.Contains(ownerOverview.ByGoal, bar => bar.GoalId == goalId && bar.Count == 1);
    }

    static async Task<IReadOnlyList<StatisticsRecordView>> Records(HttpClient client, CancellationToken ct) =>
        await client.GetFromJsonAsync<StatisticsRecordView[]>("/api/statistics/records", ct) ?? [];

    static async Task<StatisticsOverview> Overview(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<StatisticsOverview>("/api/statistics/overview?days=30", ct))!;

    static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> until, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var value = await read();

            if (until(value))
            {
                return value;
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException("the projection never caught up");
    }
}
