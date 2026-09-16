using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;
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

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            Sent.Add(envelopes);
            return Task.FromResult(Respond(envelopes));
        }

        public Task<SyncResponse?> SyncAsync(long since) => Task.FromResult<SyncResponse?>(Pull);
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

    static SyncService ServiceFor(ISyncApi api, IOutbox outbox) =>
        new(api, new InMemoryReplica(), outbox);

    static CommandEnvelope Envelope() =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0)));
}
