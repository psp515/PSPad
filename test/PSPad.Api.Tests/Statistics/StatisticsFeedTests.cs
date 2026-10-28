using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using static PSPad.Api.Tests.Statistics.StatisticsFeed;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class StatisticsFeedTests(MongoFixture fixture)
{
    [Fact]
    public async Task ACompletedTaskKeepsTheNameItHadWhenItWasCompleted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new CompleteTask(Guid.NewGuid(), user, taskId),
            new RenameTask(Guid.NewGuid(), user, taskId, "Buy oat milk"));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Completed"), ct);

        Assert.Equal("Buy milk", Assert.Single(feed, record => record.Kind == "Completed").TaskName);
    }

    [Fact]
    public async Task CompletingTheSameTaskAgainCountsAsTheSecondTime()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Water the plants"),
            new CompleteTask(Guid.NewGuid(), user, taskId),
            new ReopenTask(Guid.NewGuid(), user, taskId),
            new CompleteTask(Guid.NewGuid(), user, taskId));

        var feed = await FeedWhereAsync(client, records => records.Count(record => record.Kind == "Completed") == 2, ct);

        var completions = feed.Where(record => record.Kind == "Completed").OrderBy(record => record.Seq);
        Assert.Equal([1, 2], completions.Select(record => record.CompletionNumber));
    }

    [Fact]
    public async Task RenamingATaskAddsNothingToTheFeed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Fix the sink"),
            new RenameTask(Guid.NewGuid(), user, taskId, "Fix the tap"),
            new CompleteTask(Guid.NewGuid(), user, taskId));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Completed"), ct);

        Assert.Equal(["Completed", "Created"], feed.Select(record => record.Kind));
    }

    [Fact]
    public async Task TickingAndUntickingARecurringOccurrenceAreTheirOwnKinds()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Read a book"),
            new SetTaskRecurrence(Guid.NewGuid(), user, taskId, RecurrenceRule.Daily(today)),
            new CompleteOccurrence(Guid.NewGuid(), user, taskId, today, true),
            new CompleteOccurrence(Guid.NewGuid(), user, taskId, today, false));

        var feed = await FeedWhereAsync(
            client, records => records.Any(record => record.Kind == "OccurrenceUnticked"), ct);

        var ticked = Assert.Single(feed, record => record.Kind == "OccurrenceTicked");
        Assert.Null(ticked.CompletionNumber);
        Assert.DoesNotContain(feed, record => record.Kind == "Completed");
    }

    [Fact]
    public async Task PuttingATaskInAGoalIsALinkRecordNamingTheGoal()
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

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "LinkedToGoal"), ct);

        var link = Assert.Single(feed, record => record.Kind == "LinkedToGoal");
        Assert.Equal(taskId, link.TaskId);
        Assert.Equal("Dune", link.TaskName);
        Assert.Equal("Read more", link.GoalName);
    }

    [Fact]
    public async Task TakingATaskOutOfAGoalIsAnUnlinkButMovingItToAnotherGoalIsNot()
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
            new CreateGoal(Guid.NewGuid(), user, second, "Eat better"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Dune"),
            new AddTaskToGoal(Guid.NewGuid(), user, first, taskId),
            new AddTaskToGoal(Guid.NewGuid(), user, second, taskId),
            new RemoveTaskFromGoal(Guid.NewGuid(), user, second, taskId));

        var feed = await FeedWhereAsync(
            client, records => records.Count(record => record.Kind == "LinkedToGoal") == 3, ct);

        Assert.Equal(
            ["Read more", "Eat better", null],
            feed.Where(record => record.Kind == "LinkedToGoal").OrderBy(record => record.Seq)
                .Select(record => record.GoalName));
    }

    [Fact]
    public async Task AVisitorsSnapshotTickIsNotActivity()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"));
        var published = await (await client.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots", new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(1)), ct))
            .Content.ReadFromJsonAsync<PublishedSnapshotView>(ct);
        var visitor = factory.CreateClient();
        (await visitor.PostAsJsonAsync(
            $"/api/public/snapshots/{published!.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct)).EnsureSuccessStatusCode();
        await SendAsync(client, ct, new CompleteTask(Guid.NewGuid(), user, taskId));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Completed"), ct);

        Assert.Equal(["Completed", "Created"], feed.Select(record => record.Kind));
    }

    [Fact]
    public async Task ARecordNamesItsListAndShowsWhereTheTaskStandsNow()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new CompleteTask(Guid.NewGuid(), user, taskId));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Completed"), ct);

        var completed = Assert.Single(feed, record => record.Kind == "Completed");
        Assert.Equal("Errands", completed.ListName);
        Assert.Equal("Done", completed.CurrentStatus);
    }

    [Fact]
    public async Task ADeletedListKeepsItsNameInTheFeed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new CompleteTask(Guid.NewGuid(), user, taskId),
            new DeleteTaskList(Guid.NewGuid(), user, listId));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Deleted"), ct);

        Assert.Equal("Errands", Assert.Single(feed, record => record.Kind == "Completed").ListName);
    }

    [Fact]
    public async Task ARenamedListShowsItsNewNameOnOlderRecords()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new CompleteTask(Guid.NewGuid(), user, taskId),
            new RenameTaskList(Guid.NewGuid(), user, listId, "Shopping"));

        var feed = await FeedWhereAsync(
            client, records => records.Any(record => record.Kind == "Completed" && record.ListName == "Shopping"), ct);

        Assert.Equal("Shopping", Assert.Single(feed, record => record.Kind == "Completed").ListName);
    }

    [Fact]
    public async Task AMembersFeedNamesTheOwnersList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await SignInAsync(member, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        var taskId = Guid.NewGuid();
        await SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));
        await SendAsync(member, ct, new CompleteTask(Guid.NewGuid(), memberId, taskId));

        var feed = await FeedWhereAsync(member, records => records.Any(record => record.Kind == "Completed"), ct);

        Assert.Equal("Książki", Assert.Single(feed, record => record.Kind == "Completed").ListName);
    }

    [Fact]
    public async Task ARecordWithNoStoredNameBorrowsTheLiveTaskName()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy oat milk"));
        await SeedUnnamedRecordAsync(factory, user, taskId, ct);

        var feed = await RecordsAsync(client, ct);

        var legacy = Assert.Single(feed, record => record.Seq == long.MaxValue - 1);
        Assert.Equal("Buy oat milk", legacy.TaskName);
        Assert.Equal("Open", legacy.CurrentStatus);
    }

    [Fact]
    public async Task ARecordWithNoStoredNameOfAGoneTaskReadsAsADeletedTask()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        await SeedUnnamedRecordAsync(factory, user, Guid.NewGuid(), ct);

        var legacy = Assert.Single(await RecordsAsync(client, ct));

        Assert.Equal("(deleted task)", legacy.TaskName);
        Assert.Equal("Gone", legacy.CurrentStatus);
    }

    [Fact]
    public async Task TheFeedIsCappedAtTwoHundredAndDefaultsToFifty()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        await CreateTasksAsync(client, user, listId, 210, ct);
        await EventuallyAsync(
            () => OverviewAsync(client, ct), overview => overview.Tiles.OpenedToday == 210, ct);

        Assert.Equal(200, (await RecordsAsync(client, ct, "?limit=5000")).Count);
        Assert.Equal(50, (await RecordsAsync(client, ct, "?limit=0")).Count);
        Assert.Equal(7, (await RecordsAsync(client, ct, "?limit=7")).Count);
    }

    static async Task CreateTasksAsync(HttpClient client, Guid user, Guid listId, int count, CancellationToken ct)
    {
        foreach (var batch in Enumerable.Range(0, count).Chunk(CommandBatch.MaxEnvelopes))
        {
            await SendAsync(client, ct, batch
                .Select(index => (object)new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, $"Task {index}"))
                .ToArray());
        }
    }

    static async Task SeedUnnamedRecordAsync(ApiFactory factory, Guid user, Guid taskId, CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IStatisticsStore>().SaveAsync(new StatisticsRecord
        {
            Id = StatisticsRecord.IdFor(long.MaxValue - 1, user),
            Seq = long.MaxValue - 1,
            UserId = user,
            At = DateTimeOffset.UtcNow,
            Kind = RecordKind.Completed,
            TaskId = taskId,
            TaskName = ""
        }, ct);
    }
}
