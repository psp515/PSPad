using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class StatisticsEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task CompletingATaskShowsUpInTheRecordFeed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await UserId(client, ct);
        var taskId = await SeedTask(client, user, "Buy milk", ct);
        await Post(client, new CompleteTask(Guid.NewGuid(), user, taskId), ct);

        var records = await EventuallyAsync(
            () => Records(client, "", ct),
            feed => feed.Any(record => record.Kind == "Completed"),
            ct);

        var completed = Assert.Single(records, record => record.Kind == "Completed");
        Assert.Equal(taskId, completed.TaskId);
        Assert.Equal("Buy milk", completed.TaskName);
        Assert.Equal("Errands", completed.ListName);
        Assert.Equal(1, completed.CompletionNumber);
    }

    [Fact]
    public async Task AFeedRecordKeepsItsNameAfterTheTaskIsDeleted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await UserId(client, ct);
        var taskId = await SeedTask(client, user, "Renew the passport", ct);
        await Post(client, new CompleteTask(Guid.NewGuid(), user, taskId), ct);
        await Post(client, new DeleteTask(Guid.NewGuid(), user, taskId), ct);

        var records = await EventuallyAsync(
            () => Records(client, "", ct),
            feed => feed.Any(record => record.Kind == "Deleted"),
            ct);

        var completed = Assert.Single(records, record => record.Kind == "Completed");
        Assert.Equal("Renew the passport", completed.TaskName);
    }

    [Fact]
    public async Task ADeletedTasksRecordReportsItsCurrentStatusAsGone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await UserId(client, ct);
        var taskId = await SeedTask(client, user, "Book the ferry", ct);
        await Post(client, new DeleteTask(Guid.NewGuid(), user, taskId), ct);

        var records = await EventuallyAsync(
            () => Records(client, "", ct),
            feed => feed.Any(record => record.Kind == "Deleted"),
            ct);

        Assert.All(records, record => Assert.Equal("Gone", record.CurrentStatus));
    }

    [Fact]
    public async Task TheOverviewCountsTodaysCompletion()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await UserId(client, ct);
        var taskId = await SeedTask(client, user, "Sharpen the saw", ct);
        await Post(client, new CompleteTask(Guid.NewGuid(), user, taskId), ct);

        var overview = await EventuallyAsync(
            () => Overview(client, "?days=30", ct),
            view => view.Tiles.DoneToday > 0,
            ct);

        Assert.Equal(1, overview.Tiles.DoneToday);
        Assert.Equal(1, overview.Tiles.OpenedToday);
        Assert.Equal(1, overview.Tiles.DoneThisWeek);
        Assert.Equal(30, overview.Completions.Count);
        Assert.Equal(30, overview.Opened.Count);
        Assert.Equal(30, overview.Outstanding.Count);
        Assert.Equal(30, overview.Heatmap.Count);
        Assert.Equal(1, overview.Completions[^1].Unplanned);
        Assert.Contains(overview.ByGoal, bar => bar.GoalId is null && bar.Count == 1);
    }

    [Fact]
    public async Task ACapturedInboxItemWeighsOnTheBacklogUntilItIsOrganised()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await UserId(client, ct);
        var inboxId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var kept = Guid.NewGuid();
        await Post(client, new CreateInbox(Guid.NewGuid(), user, inboxId), ct);
        await Post(client, new CaptureToInbox(Guid.NewGuid(), user, inboxId, itemId, "Ring the plumber"), ct);
        await Post(client, new CaptureToInbox(Guid.NewGuid(), user, inboxId, kept, "Read the manual"), ct);

        var captured = await EventuallyAsync(
            () => Overview(client, "?days=30", ct),
            view => view.InboxBacklog[^1].Count == 2,
            ct);

        Assert.InRange(captured.InboxBacklog.Count, 5, 6);

        await Post(
            client,
            new OrganiseInboxItem(Guid.NewGuid(), user, inboxId, itemId, Guid.NewGuid(), Guid.NewGuid()),
            ct);

        var organised = await EventuallyAsync(
            () => Overview(client, "?days=30", ct),
            view => view.InboxBacklog[^1].Count == 1,
            ct);

        Assert.Equal(1, organised.InboxBacklog[^1].Count);
    }

    [Fact]
    public async Task AnotherUsersWorkIsInvisibleInTheFeedAndTheOverview()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var theirs = factory.ClientFor(Guid.NewGuid().ToString());
        var them = await UserId(theirs, ct);
        var theirTaskId = await SeedTask(theirs, them, "Their errand", ct);
        await Post(theirs, new CompleteTask(Guid.NewGuid(), them, theirTaskId), ct);
        await EventuallyAsync(
            () => Overview(theirs, "?days=30", ct),
            view => view.Tiles.DoneToday > 0,
            ct);

        var mine = factory.ClientFor(Guid.NewGuid().ToString());
        await UserId(mine, ct);
        var myRecords = await Records(mine, "", ct);
        var myOverview = await Overview(mine, "?days=30", ct);
        var theirRecords = await Records(theirs, "", ct);
        var theirOverview = await Overview(theirs, "?days=30", ct);

        Assert.Empty(myRecords);
        Assert.Equal(0, myOverview.Tiles.DoneToday);
        Assert.Equal(0, myOverview.Tiles.OpenedToday);
        Assert.Equal(0, myOverview.Tiles.DoneThisWeek);
        Assert.All(myOverview.Opened, point => Assert.Equal(0, point.Count));
        Assert.All(myOverview.Outstanding, point => Assert.Equal(0, point.Count));
        Assert.All(myOverview.Completions, point => Assert.Equal(0, point.Planned + point.Unplanned));
        Assert.All(myOverview.InboxBacklog, week => Assert.Equal(0, week.Count));

        Assert.Contains(theirRecords, record => record.TaskId == theirTaskId);
        Assert.Equal(1, theirOverview.Tiles.DoneToday);
        Assert.Equal(1, theirOverview.Tiles.OpenedToday);
    }

    [Fact]
    public async Task PagingWithBeforeWalksBackwards()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await UserId(client, ct);
        await SeedTask(client, user, "First", ct);
        await SeedTask(client, user, "Second", ct);
        await EventuallyAsync(
            () => Records(client, "", ct),
            feed => feed.Count == 2,
            ct);

        var first = await Records(client, "?limit=1", ct);
        var next = await Records(client, $"?limit=1&before={first[0].Seq}", ct);

        Assert.Equal("Second", first[0].TaskName);
        Assert.Equal("First", Assert.Single(next).TaskName);
        Assert.True(next[0].Seq < first[0].Seq);
    }

    static async Task<IReadOnlyList<StatisticsRecordView>> Records(
        HttpClient client, string query, CancellationToken ct) =>
        await client.GetFromJsonAsync<StatisticsRecordView[]>($"/api/statistics/records{query}", ct) ?? [];

    static async Task<StatisticsOverview> Overview(
        HttpClient client, string query, CancellationToken ct) =>
        (await client.GetFromJsonAsync<StatisticsOverview>($"/api/statistics/overview{query}", ct))!;

    static async Task<T> EventuallyAsync<T>(
        Func<Task<T>> read, Func<T, bool> until, CancellationToken ct)
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

    static async Task<Guid> UserId(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

    static async Task<Guid> SeedTask(HttpClient client, Guid user, string name, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands", 0)),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskId, listId, name))
        }, ct);

        return taskId;
    }

    static Task Post<T>(HttpClient client, T command, CancellationToken ct) where T : notnull =>
        client.PostAsJsonAsync("/api/commands", new[] { Envelope(command) }, ct);

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
