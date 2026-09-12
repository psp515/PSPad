using System.Net.Http.Json;
using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class OfflineRoundTripTests(MongoFixture fixture)
{
    [Fact]
    public async Task EditsMadeOfflineReachTheServerInOrderWhenSyncRuns()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;

        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands", 0)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk")));

        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);

        var outcome = await sync.SyncAsync(ct);

        Assert.Equal(3, outcome.Pushed);
        Assert.Empty(outcome.Rejections);
        Assert.Equal(0, await outbox.CountAsync());
        Assert.Equal("Buy milk", (await replica.LoadAsync<TodoTask>(taskId))!.Name);
        Assert.True(await replica.MarkerAsync() > 0);
    }

    [Fact]
    public async Task ASecondSyncAfterNoChangesPushesAndPullsNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), me!.UserId, Guid.NewGuid(), "Home", 0)));
        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);
        await sync.SyncAsync(ct);

        var second = await sync.SyncAsync(ct);

        Assert.Equal(0, second.Pushed);
        Assert.Equal(0, second.Pulled);
    }

    [Fact]
    public async Task ARejectedCommandHoldsTheOnesBehindIt()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var outbox = new InMemoryOutbox();

        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new RenameArea(Guid.NewGuid(), user, Guid.NewGuid(), "Nowhere")));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)));

        var outcome = await new SyncService(new HttpSyncApi(client), new InMemoryReplica(), outbox)
            .SyncAsync(ct);

        Assert.Equal(0, outcome.Pushed);
        Assert.Single(outcome.Rejections);
        Assert.Equal(2, await outbox.CountAsync());
    }

    sealed class HttpSyncApi(HttpClient http) : ISyncApi
    {
        public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            var response = await http.PostAsJsonAsync("/api/commands", envelopes);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
        }

        public Task<SyncResponse?> SyncAsync(long since) =>
            http.GetFromJsonAsync<SyncResponse>($"/api/sync?since={since}");
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
