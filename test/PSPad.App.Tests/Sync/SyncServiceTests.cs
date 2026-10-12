using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class SyncServiceTests
{
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeApi : ISyncApi
    {
        public List<IReadOnlyList<CommandEnvelope>> Sent { get; } = [];

        public Func<IReadOnlyList<CommandEnvelope>, IReadOnlyList<CommandResponse>> Respond { get; set; } =
            envelopes => envelopes.Select(_ => new CommandResponse(Guid.NewGuid(), true, null)).ToArray();

        public SyncResponse Pull { get; set; } =
            new(0, new Dictionary<string, JsonElement[]>(), []);

        public Queue<SyncResponse> Pulls { get; } = [];

        public long? RequestedSince { get; private set; }

        public List<long> RequestedSinces { get; } = [];

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            Sent.Add(envelopes);
            return Task.FromResult(Respond(envelopes));
        }

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
        {
            RequestedSince = since;
            RequestedSinces.Add(since);
            return Task.FromResult<SyncResponse?>(Pulls.Count > 0 ? Pulls.Dequeue() : Pull);
        }

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    [Fact]
    public async Task NothingIsSentWhenTheOutboxIsEmpty()
    {
        var api = new FakeApi();
        var outcome = await ServiceFor(api, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Empty(api.Sent);
        Assert.Equal(0, outcome.Pushed);
    }

    [Fact]
    public async Task AcceptedCommandsLeaveTheOutbox()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi();

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(2, outcome.Pushed);
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task TheOutboxStopsAtTheFirstRejectionAndKeepsWhatFollows()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi
        {
            Respond = envelopes =>
            [
                new CommandResponse(Guid.NewGuid(), true, null),
                new CommandResponse(Guid.NewGuid(), false, "That list no longer exists."),
                new CommandResponse(Guid.NewGuid(), true, null)
            ]
        };

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pushed);
        Assert.Equal("That list no longer exists.", Assert.Single(outcome.Rejections));
        Assert.Equal(2, await outbox.CountAsync());
    }

    [Fact]
    public async Task TheOutcomeNamesTheRejectedCommand()
    {
        var outbox = new InMemoryOutbox();
        var rejected = Guid.NewGuid();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(rejected, Envelope());
        var api = new FakeApi
        {
            Respond = envelopes =>
            [
                new CommandResponse(Guid.NewGuid(), true, null),
                new CommandResponse(Guid.NewGuid(), false, "That list no longer exists.")
            ]
        };

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(rejected, Assert.Single(outcome.RejectedCommands));
    }

    [Fact]
    public async Task AnUnrecoverableRejectionIsDroppedInsteadOfWedgingTheOutbox()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi
        {
            Respond = envelopes =>
            [
                new CommandResponse(Guid.NewGuid(), true, null),
                new CommandResponse(Guid.NewGuid(), false, "That command is for a different user.", Unrecoverable: true),
                new CommandResponse(Guid.NewGuid(), true, null)
            ]
        };

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pushed);
        Assert.Equal("That command is for a different user.", Assert.Single(outcome.Rejections));
        Assert.Equal(1, await outbox.CountAsync());
    }

    [Fact]
    public async Task PulledDocumentsOverwriteTheReplicaAndAdvanceTheMarker()
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0), DateTimeOffset.UnixEpoch));
        var replica = new InMemoryReplica();
        var api = new FakeApi
        {
            Pull = new SyncResponse(
                17,
                new Dictionary<string, JsonElement[]>
                {
                    ["areas"] = [JsonSerializer.SerializeToElement(area)]
                },
                [])
        };

        var outcome = await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pulled);
        Assert.Equal(17, await replica.MarkerAsync());
        Assert.Equal("Home", (await replica.LoadAsync<Area>(area.Id))!.Name);
    }

    [Fact]
    public async Task APulledAreaViewLandsInTheReplica()
    {
        var replica = new InMemoryReplica();
        var list = Guid.NewGuid();
        var view = new AreaView();
        view.Apply(new ListsReordered(Guid.NewGuid(), User, DateTimeOffset.UnixEpoch, Guid.NewGuid(), [list]));
        var api = new FakeApi
        {
            Pull = new(5, new Dictionary<string, JsonElement[]>
            {
                ["areaviews"] = [JsonSerializer.SerializeToElement(view, new JsonSerializerOptions(JsonSerializerDefaults.Web))]
            }, [])
        };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<AreaView>(view.Id);
        Assert.Equal([list], stored!.Order);
    }

    [Fact]
    public async Task AStoredFingerprintMissingANewCollectionPullsFromZeroOnce()
    {
        var replica = new InMemoryReplica();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync("areas,goals,inboxes,tasklists,todotasks");
        var api = new FakeApi();

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(0, api.RequestedSince);
    }

    [Fact]
    public async Task AMatchingStoredFingerprintPullsFromTheStoredMarker()
    {
        var replica = new InMemoryReplica();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        var api = new FakeApi();

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(42, api.RequestedSince);
    }

    [Fact]
    public async Task APullStoresTheCurrentFingerprintAlongsideTheMarker()
    {
        var replica = new InMemoryReplica();
        var api = new FakeApi();

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(SyncService.CollectionsFingerprint, await replica.CollectionsFingerprintAsync());
    }

    [Fact]
    public async Task AFirstDatasetIdIsRecordedWithoutAReset()
    {
        var replica = new InMemoryReplica();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        var dataset = Guid.NewGuid();
        var api = new FakeApi { Pull = EmptyPull(50, dataset) };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal([42L], api.RequestedSinces);
        Assert.Equal(dataset, await replica.DatasetIdAsync());
        Assert.Equal(50, await replica.MarkerAsync());
    }

    [Fact]
    public async Task ANewDatasetIdResetsTheReplicaAndPullsFromZero()
    {
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(User);
        await replica.SetMarkerAsync(40);
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        await replica.SetDatasetIdAsync(Guid.NewGuid());
        var stale = new Area();
        stale.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Gone", 0), DateTimeOffset.UnixEpoch));
        await replica.SaveAsync(stale);
        var restored = Guid.NewGuid();
        var api = new FakeApi();
        api.Pulls.Enqueue(EmptyPull(40, restored));
        api.Pulls.Enqueue(EmptyPull(12, restored));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal([40L, 0L], api.RequestedSinces);
        Assert.Null(await replica.LoadAsync<Area>(stale.Id));
        Assert.Equal(restored, await replica.DatasetIdAsync());
        Assert.Equal(User, await replica.OwnerAsync());
        Assert.Equal(12, await replica.MarkerAsync());
    }

    [Fact]
    public async Task AnUnchangedDatasetIdChangesNothing()
    {
        var replica = new InMemoryReplica();
        var dataset = Guid.NewGuid();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        await replica.SetDatasetIdAsync(dataset);
        var api = new FakeApi { Pull = EmptyPull(43, dataset) };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal([42L], api.RequestedSinces);
        Assert.Equal(43, await replica.MarkerAsync());
    }

    [Fact]
    public async Task AResponseWithoutADatasetIdChangesNothing()
    {
        var replica = new InMemoryReplica();
        var dataset = Guid.NewGuid();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        await replica.SetDatasetIdAsync(dataset);
        var api = new FakeApi { Pull = EmptyPull(43, null) };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal([42L], api.RequestedSinces);
        Assert.Equal(dataset, await replica.DatasetIdAsync());
    }

    static SyncResponse EmptyPull(long marker, Guid? dataset) =>
        new(marker, new Dictionary<string, JsonElement[]>(), [], DatasetId: dataset);

    static SyncService ServiceFor(ISyncApi api, IOutbox outbox) =>
        new(api, new InMemoryReplica(), outbox);

    static CommandEnvelope Envelope() =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0)));
}
