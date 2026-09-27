using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
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
        Assert.Single(sync.Events, @event => @event.Type == nameof(AreaDeleted));
        Assert.Equal(2, sync.Events.Count(@event => @event.Type == nameof(TaskListDeleted)));
        Assert.Equal(3, sync.Events.Count(@event => @event.Type == nameof(TaskDeleted)));
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
        var sync = (await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={marker}", ct))!;
        Assert.Empty(sync.Events);
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

    static async Task<(Guid AreaId, Guid[] ListIds, Guid[] TaskIds)> SeedAreaAsync(
        HttpClient client, Guid user, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        Guid[] listIds = [Guid.NewGuid(), Guid.NewGuid()];
        Guid[] taskIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await PostAsync(client, ct,
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Cascade", 99)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listIds[0], areaId, "Errands", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listIds[1], areaId, "Chores", 1)),
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
