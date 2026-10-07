using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class SyncCoordinatorTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public async Task ThePulledRevisionAdvancesWhenSyncBringsDocumentsDown()
    {
        // A screen rendered from an empty replica -- the state a fresh sign-in leaves behind --
        // has nothing else to tell it that its data has since arrived.
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        Assert.Equal(0, coordinator.Revision);

        await coordinator.SyncNowAsync();

        Assert.Equal(1, coordinator.Revision);
    }

    [Fact]
    public async Task ThePulledRevisionStandsStillWhenNothingCameDown()
    {
        // Polling every minute must not make every open screen re-read the replica for nothing.
        var coordinator = CoordinatorFor(new SyncResponse(7, new Dictionary<string, JsonElement[]>(), []));

        await coordinator.SyncNowAsync();
        await coordinator.SyncNowAsync();

        Assert.Equal(0, coordinator.Revision);
    }

    [Fact]
    public async Task ThePulledRevisionStandsStillWhileOffline()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"), online: false);

        await coordinator.SyncNowAsync();

        Assert.Equal(0, coordinator.Revision);
    }

    [Fact]
    public async Task EverySyncThatLandsAdvancesItAgain()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();
        await coordinator.SyncNowAsync();

        Assert.Equal(2, coordinator.Revision);
    }

    [Fact]
    public async Task StartingHandsBackTheFirstPullSoTheShellCanWaitForIt()
    {
        // The shell has nothing to render on a device holding none of this user's data, so it
        // needs something to await rather than a notification it has to hope arrives.
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        coordinator.Start();
        await coordinator.Started;

        Assert.Equal(1, coordinator.Revision);
    }

    [Fact]
    public async Task StartingAgainHandsBackAFreshPullRatherThanTheOneFromBeforeTheSignIn()
    {
        // AuthorizeRouteView renders the anonymous redirect inside AppShell, so the shell starts
        // the coordinator once while signed out and again once signed in. A latched first pull
        // would hand the signed-in shell a task that had already finished with no session.
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        coordinator.Start();
        await coordinator.Started;

        coordinator.Start();
        await coordinator.Started;

        Assert.Equal(2, coordinator.Revision);
    }

    [Fact]
    public void ThereIsSomethingToAwaitEvenBeforeAnythingHasStarted()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        Assert.True(coordinator.Started.IsCompleted);
    }

    [Fact]
    public async Task ASyncThatReachesTheServerStampsTheTimeAndKeepsIt()
    {
        var replica = new InMemoryReplica();
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"), replica: replica);

        await coordinator.SyncNowAsync();

        Assert.Equal(Noon, coordinator.LastSyncedAt);
        Assert.Equal(Noon, await replica.LastSyncedAtAsync());
    }

    [Fact]
    public async Task ASyncThatNeverReachedTheServerLeavesTheStampAlone()
    {
        var coordinator = CoordinatorFor((SyncResponse?)null);

        await coordinator.SyncNowAsync();

        Assert.Null(coordinator.LastSyncedAt);
    }

    [Fact]
    public async Task AServerThatIsDownLeavesTheStampAlone()
    {
        var replica = new InMemoryReplica();
        await replica.SetLastSyncedAtAsync(Noon.AddMinutes(-5));
        var coordinator = CoordinatorFor(new HttpRequestException("down"), replica: replica);
        await coordinator.LoadLastSyncedAtAsync();

        await coordinator.SyncNowAsync();

        Assert.Equal(Noon.AddMinutes(-5), coordinator.LastSyncedAt);
    }

    [Fact]
    public async Task ASyncThatFailsIsFlaggedAndTheNextGoodOneClearsIt()
    {
        var down = CoordinatorFor(new HttpRequestException("down"));
        await down.SyncNowAsync();
        Assert.True(down.LastSyncFailed);

        var up = CoordinatorFor(AnAreaCalled("Dom"));
        await up.SyncNowAsync();
        Assert.False(up.LastSyncFailed);
    }

    [Fact]
    public async Task OfflineLeavesTheStampAlone()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"), online: false);

        await coordinator.SyncNowAsync();

        Assert.Null(coordinator.LastSyncedAt);
    }

    [Fact]
    public async Task AColdStartReadsTheKeptStampBack()
    {
        var replica = new InMemoryReplica();
        await replica.SetLastSyncedAtAsync(Noon.AddDays(-3));
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"), replica: replica);

        await coordinator.LoadLastSyncedAtAsync();

        Assert.Equal(Noon.AddDays(-3), coordinator.LastSyncedAt);
    }

    [Fact]
    public async Task ItIsSyncingOnlyWhileARunIsInFlight()
    {
        Services.AddMudServices();
        var outbox = new InMemoryOutbox();
        var api = new GatedApi(AnAreaCalled("Dom"));
        var replica = new InMemoryReplica();
        var coordinator = new SyncCoordinator(
            new SyncService(api, replica, outbox), new FixedConnectivity(true), outbox,
            Services.GetRequiredService<ISnackbar>(), replica, new StoppedClock(Noon));
        var changes = 0;
        coordinator.Changed += () => changes++;

        var run = coordinator.SyncNowAsync();

        Assert.True(coordinator.IsSyncing);
        api.Release();
        await run;
        Assert.False(coordinator.IsSyncing);
        Assert.True(changes >= 2);
    }

    [Fact]
    public async Task OverlappingCallsShareOneRunAndQueueExactlyOneFollowUpPass()
    {
        // CommandSender will call this after every command; two commands queued in quick
        // succession must not run SyncService.SyncAsync twice in parallel -- PushAsync peeks
        // the outbox without removing entries until the response lands, so a second concurrent
        // push would peek and resend the same batch.
        Services.AddMudServices();
        var outbox = new InMemoryOutbox();
        var api = new GatedApi(AnAreaCalled("Dom"));
        var coordinator = new SyncCoordinator(
            new SyncService(api, new InMemoryReplica(), outbox),
            new FixedConnectivity(true),
            outbox,
            Services.GetRequiredService<ISnackbar>(),
            new InMemoryReplica(),
            new StoppedClock(Noon));

        var first = coordinator.SyncNowAsync();
        var second = coordinator.SyncNowAsync();

        Assert.Same(first, second);
        Assert.Equal(1, api.SyncCalls);

        api.Release();
        await first;

        Assert.Equal(2, api.SyncCalls);
    }

    static SyncResponse AnAreaCalled(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));

        return new SyncResponse(
            1,
            new Dictionary<string, JsonElement[]>
            {
                ["areas"] =
                [
                    JsonSerializer.SerializeToElement(
                        area, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ]
            },
            []);
    }

    SyncCoordinator CoordinatorFor(HttpRequestException failure, InMemoryReplica? replica = null)
    {
        var local = replica ?? new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        Services.AddMudServices();
        return new SyncCoordinator(
            new SyncService(new FakeApi(null, failure), local, outbox), new FixedConnectivity(true), outbox,
            Services.GetRequiredService<ISnackbar>(), local, new StoppedClock(Noon));
    }

    SyncCoordinator CoordinatorFor(SyncResponse? pull, bool online = true, InMemoryReplica? replica = null)
    {
        Services.AddMudServices();

        replica ??= new InMemoryReplica();
        var outbox = new InMemoryOutbox();

        return new SyncCoordinator(
            new SyncService(new FakeApi(pull), replica, outbox),
            new FixedConnectivity(online),
            outbox,
            Services.GetRequiredService<ISnackbar>(),
            replica,
            new StoppedClock(Noon));
    }

    static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    sealed class StoppedClock(DateTimeOffset now) : PSPad.Abstractions.IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    sealed class FakeApi(SyncResponse? pull, HttpRequestException? failure = null) : ISyncApi
    {
        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>(
                [.. envelopes.Select(_ => new CommandResponse(Guid.NewGuid(), true, null))]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
            failure is null ? Task.FromResult(pull) : Task.FromException<SyncResponse?>(failure);

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    sealed class FixedConnectivity(bool online) : IConnectivity
    {
        public bool IsOnline => online;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }

    sealed class GatedApi(SyncResponse pull) : ISyncApi
    {
        readonly TaskCompletionSource _gate = new();

        public int SyncCalls { get; private set; }

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public async Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
        {
            SyncCalls++;
            await _gate.Task;
            return pull;
        }

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());

        public void Release() => _gate.SetResult();
    }
}
