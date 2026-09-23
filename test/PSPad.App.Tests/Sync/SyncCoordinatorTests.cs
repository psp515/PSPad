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

    SyncCoordinator CoordinatorFor(SyncResponse pull, bool online = true)
    {
        Services.AddMudServices();

        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();

        return new SyncCoordinator(
            new SyncService(new FakeApi(pull), replica, outbox),
            new FixedConnectivity(online),
            outbox,
            Services.GetRequiredService<ISnackbar>());
    }

    sealed class FakeApi(SyncResponse pull) : ISyncApi
    {
        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>(
                [.. envelopes.Select(_ => new CommandResponse(Guid.NewGuid(), true, null))]);

        public Task<SyncResponse?> SyncAsync(long since) => Task.FromResult<SyncResponse?>(pull);
    }

    sealed class FixedConnectivity(bool online) : IConnectivity
    {
        public bool IsOnline => online;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }
}
