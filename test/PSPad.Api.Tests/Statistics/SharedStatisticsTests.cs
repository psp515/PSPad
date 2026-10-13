using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Statistics;
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
        await Sharing.SendAsync(owner, ct, new AddTaskToGoal(Guid.NewGuid(), ownerId, goalId, taskId));

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

    [Fact]
    public async Task AMemberCannotPutATaskIntoTheOwnersGoal()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ct);
        var goalId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateGoal(Guid.NewGuid(), ownerId, goalId, "Private plans"));
        await Sharing.SendAsync(member, ct, new CreateTask(Guid.NewGuid(), memberId, taskId, listId, "Mine"));

        var result = Assert.Single(await Send(member, ct, new AddTaskToGoal(Guid.NewGuid(), memberId, goalId, taskId)));

        Assert.False(result.Accepted);
        Assert.Equal("That goal belongs to somebody else.", result.Rejection);
    }

    [Fact]
    public async Task AMemberCountsASharedTaskTowardTheirOwnGoal()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ct);
        var membersGoal = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));
        await Sharing.SendAsync(member, ct,
            new CreateGoal(Guid.NewGuid(), memberId, membersGoal, "Read more"),
            new AddTaskToGoal(Guid.NewGuid(), memberId, membersGoal, taskId),
            new CompleteTask(Guid.NewGuid(), memberId, taskId));

        await EventuallyAsync(
            () => Records(member, ct),
            feed => feed.Any(record => record.Kind == "Completed" && record.TaskId == taskId),
            ct);

        Assert.Contains((await Overview(member, ct)).ByGoal, bar => bar.GoalId == membersGoal && bar.Count == 1);
        Assert.DoesNotContain((await Overview(owner, ct)).ByGoal, bar => bar.GoalId == membersGoal);
    }

    [Fact]
    public async Task AnOldRecordNamingTheOwnersGoalNeverShowsItsNameToAMember()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, _) = await SharedAsync(factory, ct);
        var goalId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateGoal(Guid.NewGuid(), ownerId, goalId, "Private plans"));
        using var scope = factory.Services.CreateScope();
        var labels = scope.ServiceProvider.GetRequiredService<ILabelStore>();
        await EventuallyAsync(() => labels.FindAsync(goalId, ct), label => label is not null, ct);
        await scope.ServiceProvider.GetRequiredService<IStatisticsStore>().SaveAsync(new StatisticsRecord
        {
            Id = StatisticsRecord.IdFor(long.MaxValue - 1, memberId),
            Seq = long.MaxValue - 1,
            UserId = memberId,
            At = DateTimeOffset.UtcNow,
            Kind = RecordKind.LinkedToGoal,
            TaskId = Guid.NewGuid(),
            TaskName = "Mine",
            GoalId = goalId
        }, ct);

        var record = Assert.Single(await Records(member, ct), candidate => candidate.Kind == "LinkedToGoal");

        Assert.Null(record.GoalName);
    }

    static async Task<(HttpClient Owner, Guid OwnerId, HttpClient Member, Guid MemberId, Guid ListId)> SharedAsync(
        ApiFactory factory, CancellationToken ct)
    {
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        return (owner, ownerId, member, memberId, listId);
    }

    static async Task<CommandResponse[]> Send(HttpClient client, CancellationToken ct, params ICommand[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        return (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
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
