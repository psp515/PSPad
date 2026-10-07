using System.Net.Http.Json;
using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
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
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands")));
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
    public async Task ACapturedInboxItemSurvivesTheRoundTripThroughTheServer()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;

        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var inboxId = Guid.NewGuid();

        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateInbox(Guid.NewGuid(), user, inboxId)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CaptureToInbox(Guid.NewGuid(), user, inboxId, Guid.NewGuid(), "Buy milk")));

        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);

        var outcome = await sync.SyncAsync(ct);

        Assert.Equal(2, outcome.Pushed);
        Assert.Empty(outcome.Rejections);
        Assert.Contains((await replica.LoadAsync<Inbox>(inboxId))!.Items, item => item.Text == "Buy milk");
    }

    [Fact]
    public async Task AStepAddedToATaskSurvivesTheRoundTripThroughTheServer()
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
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands")));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk")));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new AddStep(Guid.NewGuid(), user, taskId, Guid.NewGuid(), "Bring reusable bag")));

        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);

        var outcome = await sync.SyncAsync(ct);

        Assert.Equal(4, outcome.Pushed);
        Assert.Empty(outcome.Rejections);
        Assert.Contains(
            (await replica.LoadAsync<TodoTask>(taskId))!.Steps,
            step => step.Name == "Bring reusable bag");
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

    [Fact]
    public async Task ReferenceCommandsQueuedOfflineLandOnTheServer()
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
        var itemId = Guid.NewGuid();
        var colourField = Guid.NewGuid();
        var weightField = Guid.NewGuid();

        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", ListKind.Reference)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateReferenceItem(Guid.NewGuid(), user, itemId, listId, "PLA Black", 0)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new AddReferenceField(Guid.NewGuid(), user, itemId, colourField, "Colour", "Black", null)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new AddReferenceField(Guid.NewGuid(), user, itemId, weightField, "Left", "350 g", "quantity")));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new SetReferenceItemDescription(Guid.NewGuid(), user, itemId, "Dry 4h at 50 °C")));

        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);

        var outcome = await sync.SyncAsync(ct);

        Assert.Equal(6, outcome.Pushed);
        Assert.Empty(outcome.Rejections);
        var item = await replica.LoadAsync<ReferenceItem>(itemId);
        Assert.Equal("PLA Black", item!.Name);
        Assert.Equal("Dry 4h at 50 °C", item.Description);
        Assert.Equal(["Colour", "Left"], item.Fields.Select(field => field.Label));
    }

    sealed class HttpSyncApi(HttpClient http) : ISyncApi
    {
        public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            var response = await http.PostAsJsonAsync("/api/commands", envelopes);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
        }

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
            http.GetFromJsonAsync<SyncResponse>(full.Count == 0
                ? $"/api/sync?since={since}"
                : $"/api/sync?since={since}&full={string.Join(',', full)}");

        public async Task<JoinOutcome> JoinAsync(string token, string code)
        {
            var response = await http.PostAsJsonAsync("/api/lists/join", new JoinListRequest(token, code));
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new JoinOutcome.Invalid();
            }

            var joined = await response.Content.ReadFromJsonAsync<JoinListResponse>();
            return new JoinOutcome.Joined(joined!.ListId, joined.Documents);
        }
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
