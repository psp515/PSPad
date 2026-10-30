using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class DeleteCascadeEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task DeletingAnAreaDeletesItsListsAndTasksInOneCommand()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, listIds, taskIds) = await SeedAreaAsync(client, user, ct);
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        var results = await PostAsync(client, ct, Envelope(new DeleteArea(Guid.NewGuid(), user, areaId)));

        Assert.True(Assert.Single(results).Accepted);
        var sync = (await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={marker}", ct))!;
        AssertDeleted(sync.Documents["areas"], [areaId]);
        AssertDeleted(sync.Documents["tasklists"], listIds);
        AssertDeleted(sync.Documents["todotasks"], taskIds);
        var events = await EventsSinceAsync(user, marker, ct);
        Assert.Single(events, @event => @event.Type == nameof(AreaDeleted));
        Assert.Equal(2, events.Count(@event => @event.Type == nameof(TaskListDeleted)));
        Assert.Equal(3, events.Count(@event => @event.Type == nameof(TaskDeleted)));
        Assert.Empty((await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct))!);
    }

    [Fact]
    public async Task ReplayingTheSameDeleteAddsNoEvents()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, _, _) = await SeedAreaAsync(client, user, ct);
        var delete = Envelope(new DeleteArea(Guid.NewGuid(), user, areaId));

        await PostAsync(client, ct, delete);
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;
        var replay = await PostAsync(client, ct, delete);

        Assert.True(Assert.Single(replay).Accepted);
        Assert.Empty(await EventsSinceAsync(user, marker, ct));
    }

    [Fact]
    public async Task CreatingATaskInADeletedListIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (_, listIds, _) = await SeedAreaAsync(client, user, ct);
        await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[0])));

        var results = await PostAsync(client, ct,
            Envelope(new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listIds[0], "too late")));

        var result = Assert.Single(results);
        Assert.False(result.Accepted);
        Assert.Contains("list", result.Rejection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MovingAListIntoADeletedAreaIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (_, listIds, _) = await SeedAreaAsync(client, user, ct);
        var deadAreaId = Guid.NewGuid();
        await PostAsync(client, ct,
            Envelope(new CreateArea(Guid.NewGuid(), user, deadAreaId, "Gone", 100)),
            Envelope(new DeleteArea(Guid.NewGuid(), user, deadAreaId)));

        var results = await PostAsync(client, ct,
            Envelope(new MoveTaskListToArea(Guid.NewGuid(), user, listIds[0], deadAreaId)));

        var result = Assert.Single(results);
        Assert.False(result.Accepted);
        Assert.Contains("area", result.Rejection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeletingAListDeletesItsTasksAndLeavesOtherListsAlone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (_, listIds, taskIds) = await SeedAreaAsync(client, user, ct);
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        var results = await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[0])));

        Assert.True(Assert.Single(results).Accepted);
        var sync = (await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={marker}", ct))!;
        AssertDeleted(sync.Documents["tasklists"], [listIds[0]]);
        AssertDeleted(sync.Documents["todotasks"], taskIds[..2]);
        Assert.DoesNotContain(sync.Documents["todotasks"], row => row.GetProperty("id").GetGuid() == taskIds[2]);
        var events = await EventsSinceAsync(user, marker, ct);
        Assert.Single(events, @event => @event.Type == nameof(TaskListDeleted));
        Assert.Equal(2, events.Count(@event => @event.Type == nameof(TaskDeleted)));
    }

    [Fact]
    public async Task DeletingAListDoesNotDeleteAnAlreadyDeletedTaskAgain()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (_, listIds, taskIds) = await SeedAreaAsync(client, user, ct);
        await PostAsync(client, ct, Envelope(new DeleteTask(Guid.NewGuid(), user, taskIds[0])));
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[0])));

        var events = await EventsSinceAsync(user, marker, ct);
        Assert.Equal([taskIds[1]], events.Where(@event => @event.Type == nameof(TaskDeleted)).Select(@event => @event.AggregateId));
    }

    [Fact]
    public async Task DeletingAnAlreadyDeletedListIsRejectedAndAddsNoEvents()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (_, listIds, _) = await SeedAreaAsync(client, user, ct);
        await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[0])));
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        var result = Assert.Single(await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[0]))));

        Assert.False(result.Accepted);
        Assert.Empty(await EventsSinceAsync(user, marker, ct));
    }

    [Fact]
    public async Task DeletingAnAreaLeavesOtherAreasListsAndTasksAlone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, _, _) = await SeedAreaAsync(client, user, ct);
        var otherArea = Guid.NewGuid();
        var otherList = Guid.NewGuid();
        var otherTask = Guid.NewGuid();
        await PostAsync(client, ct,
            Envelope(new CreateArea(Guid.NewGuid(), user, otherArea, "Other", 100)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, otherList, otherArea, "Keep")),
            Envelope(new CreateTask(Guid.NewGuid(), user, otherTask, otherList, "keep me")));
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        await PostAsync(client, ct, Envelope(new DeleteArea(Guid.NewGuid(), user, areaId)));

        var sync = (await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={marker}", ct))!;
        Assert.DoesNotContain(sync.Documents["areas"], row => row.GetProperty("id").GetGuid() == otherArea);
        Assert.DoesNotContain(sync.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == otherList);
        Assert.DoesNotContain(sync.Documents["todotasks"], row => row.GetProperty("id").GetGuid() == otherTask);
    }

    [Fact]
    public async Task DeletingAnAreaSkipsListsAlreadyDeleted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, listIds, _) = await SeedAreaAsync(client, user, ct);
        await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[0])));
        var marker = (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        var result = Assert.Single(await PostAsync(client, ct, Envelope(new DeleteArea(Guid.NewGuid(), user, areaId))));

        Assert.True(result.Accepted, result.Rejection);
        var events = await EventsSinceAsync(user, marker, ct);
        Assert.Equal([listIds[1]], events.Where(@event => @event.Type == nameof(TaskListDeleted)).Select(@event => @event.AggregateId));
    }

    [Fact]
    public async Task DeletingSomebodyElsesAreaIsRejectedAndTouchesNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = (await owner.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = (await stranger.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, _, _) = await SeedAreaAsync(owner, ownerId, ct);
        var marker = (await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        var result = Assert.Single(await PostAsync(stranger, ct, Envelope(new DeleteArea(Guid.NewGuid(), strangerId, areaId))));

        Assert.False(result.Accepted);
        Assert.Empty(await EventsSinceAsync(ownerId, marker, ct));
        var sync = (await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!;
        Assert.All(sync.Documents["tasklists"], row => Assert.False(row.GetProperty("deleted").GetBoolean()));
    }

    [Fact]
    public async Task MovingATaskIntoADeletedListIsRejectedAndTheTaskStays()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (_, listIds, taskIds) = await SeedAreaAsync(client, user, ct);
        await PostAsync(client, ct, Envelope(new DeleteTaskList(Guid.NewGuid(), user, listIds[1])));

        var result = Assert.Single(await PostAsync(client, ct,
            Envelope(new MoveTaskToList(Guid.NewGuid(), user, taskIds[0], listIds[1]))));

        Assert.False(result.Accepted);
        Assert.Contains("list", result.Rejection, StringComparison.OrdinalIgnoreCase);
        var task = Assert.Single(
            (await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Documents["todotasks"],
            row => row.GetProperty("id").GetGuid() == taskIds[0]);
        Assert.Equal(listIds[0], task.GetProperty("listId").GetGuid());
    }

    [Fact]
    public async Task CreatingAListInADeletedAreaIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, _, _) = await SeedAreaAsync(client, user, ct);
        await PostAsync(client, ct, Envelope(new DeleteArea(Guid.NewGuid(), user, areaId)));

        var result = Assert.Single(await PostAsync(client, ct,
            Envelope(new CreateTaskList(Guid.NewGuid(), user, Guid.NewGuid(), areaId, "Errands"))));

        Assert.False(result.Accepted);
        Assert.Contains("area", result.Rejection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatingATaskInAListThatNeverExistedIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

        var result = Assert.Single(await PostAsync(client, ct,
            Envelope(new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), Guid.NewGuid(), "buy milk"))));

        Assert.False(result.Accepted);
        Assert.Contains("list", result.Rejection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeletingAnAreaTakesItsTasksOffTheOutstandingChartWithOneRecordEach()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var (areaId, _, taskIds) = await SeedAreaAsync(client, user, ct);
        await EventuallyAsync(() => OutstandingTodayAsync(client, ct), count => count == 3, ct);

        await PostAsync(client, ct, Envelope(new DeleteArea(Guid.NewGuid(), user, areaId)));

        await EventuallyAsync(() => OutstandingTodayAsync(client, ct), count => count == 0, ct);
        var records = await EventuallyAsync(
            async () => await client.GetFromJsonAsync<StatisticsRecordView[]>("/api/statistics/records", ct) ?? [],
            feed => feed.Count(record => record.Kind == "Deleted") == 3,
            ct);
        Assert.Equal(
            taskIds.Order(),
            records.Where(record => record.Kind == "Deleted").Select(record => record.TaskId).Order());
    }


    async Task<List<StoredEvent>> EventsSinceAsync(Guid user, long since, CancellationToken ct) =>
        await Persistence.TestContext.For(fixture).Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user) &
                  Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, since))
            .ToListAsync(ct);
    static async Task<int> OutstandingTodayAsync(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<StatisticsOverview>("/api/statistics/overview?days=7", ct))!.Outstanding[^1].Count;

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

    static async Task<(Guid AreaId, Guid[] ListIds, Guid[] TaskIds)> SeedAreaAsync(
        HttpClient client, Guid user, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        Guid[] listIds = [Guid.NewGuid(), Guid.NewGuid()];
        Guid[] taskIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await PostAsync(client, ct,
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Cascade", 99)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listIds[0], areaId, "Errands")),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listIds[1], areaId, "Chores")),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskIds[0], listIds[0], "post office")),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskIds[1], listIds[0], "pharmacy")),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskIds[2], listIds[1], "vacuum")),
            Envelope(new SetTaskDueDate(Guid.NewGuid(), user, taskIds[2], today.AddDays(-1))));

        Assert.All(results, result => Assert.True(result.Accepted, result.Rejection));
        Assert.NotEmpty((await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct))!);
        return (areaId, listIds, taskIds);
    }

    static void AssertDeleted(JsonElement[] rows, IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            var row = Assert.Single(rows, candidate => candidate.GetProperty("id").GetGuid() == id);
            Assert.True(row.GetProperty("deleted").GetBoolean());
        }
    }

    static async Task<CommandResponse[]> PostAsync(HttpClient client, CancellationToken ct, params CommandEnvelope[] batch)
    {
        var response = await client.PostAsJsonAsync("/api/commands", batch, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
