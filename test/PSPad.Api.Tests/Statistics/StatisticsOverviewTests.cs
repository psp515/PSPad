using Microsoft.Extensions.DependencyInjection;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using static PSPad.Api.Tests.Statistics.StatisticsFeed;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class StatisticsOverviewTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnUnrecognisedRangeFallsBackToThirtyDays()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(client, ct);

        var overview = await OverviewAsync(client, ct, days: 7);

        Assert.Equal(30, overview.Completions.Count);
        Assert.Equal(30, overview.Heatmap.Count);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(365)]
    public async Task TheOfferedRangesAreHonoured(int days)
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(client, ct);

        var overview = await OverviewAsync(client, ct, days);

        Assert.Equal(days, overview.Opened.Count);
        Assert.Equal(days, overview.Outstanding.Count);
    }

    [Fact]
    public async Task ATaskOpenedBeforeTheWindowCountsOnEveryDayUntilItIsFinished()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var taskId = Guid.NewGuid();
        await SeedAsync(factory, ct,
            Record(user, 1, RecordKind.Created, taskId, DateTimeOffset.UtcNow.AddDays(-40)),
            Record(user, 2, RecordKind.Completed, taskId, DateTimeOffset.UtcNow));

        var overview = await OverviewAsync(client, ct);

        Assert.Equal(1, overview.Outstanding[0].Count);
        Assert.Equal(0, overview.Outstanding[^1].Count);
        Assert.All(overview.Opened, point => Assert.Equal(0, point.Count));
        Assert.Equal(-1, overview.Tiles.NetChange);
        Assert.Equal(1, overview.Tiles.DoneToday);
    }

    [Fact]
    public async Task ATaskOpenedBeforeTheWindowAndStillOpenCountsOnEveryChartedDay()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        await SeedAsync(factory, ct,
            Record(user, 1, RecordKind.Created, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-40)));

        var overview = await OverviewAsync(client, ct);

        Assert.All(overview.Outstanding, point => Assert.Equal(1, point.Count));
        Assert.All(overview.Opened, point => Assert.Equal(0, point.Count));
    }

    [Fact]
    public async Task TheGoalBarsCarryTheGoalsCurrentName()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var goalId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateGoal(Guid.NewGuid(), user, goalId, "Eat better"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new AddTaskToGoal(Guid.NewGuid(), user, goalId, taskId),
            new CompleteTask(Guid.NewGuid(), user, taskId));

        var named = await EventuallyAsync(
            () => OverviewAsync(client, ct),
            overview => overview.ByGoal.Any(bar => bar.GoalId == goalId && bar.Count == 1),
            ct);

        Assert.Equal("Eat better", Assert.Single(named.ByGoal, bar => bar.GoalId == goalId).Name);
        Assert.Contains(named.ByGoal, bar => bar.GoalId is null && bar.Count == 0);

        await SendAsync(client, ct, new RenameGoal(Guid.NewGuid(), user, goalId, "Health"));

        var renamed = await EventuallyAsync(
            () => OverviewAsync(client, ct),
            overview => overview.ByGoal.Any(bar => bar.GoalId == goalId && bar.Name == "Health"),
            ct);

        Assert.Equal(1, Assert.Single(renamed.ByGoal, bar => bar.GoalId == goalId).Count);
    }

    [Fact]
    public async Task AnInboxThatOnlyExistsAndTasksThatAreNotCapturesCountNoCaptures()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        await SendAsync(client, ct,
            new CreateInbox(Guid.NewGuid(), user, Guid.NewGuid()),
            new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, "Fix the sink"));

        var overview = await EventuallyAsync(
            () => OverviewAsync(client, ct), view => view.Tiles.OpenedToday == 1, ct);

        Assert.All(overview.InboxCaptures, week => Assert.Equal(0, week.Count));
    }

    static StatisticsRecord Record(Guid user, long seq, RecordKind kind, Guid taskId, DateTimeOffset at) =>
        new()
        {
            Id = StatisticsRecord.IdFor(long.MaxValue - 10 + seq, user),
            Seq = long.MaxValue - 10 + seq,
            UserId = user,
            At = at,
            Kind = kind,
            TaskId = taskId,
            TaskName = "task"
        };

    static async Task SeedAsync(ApiFactory factory, CancellationToken ct, params StatisticsRecord[] records)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatisticsStore>();

        foreach (var record in records)
        {
            await store.SaveAsync(record, ct);
        }
    }
}
