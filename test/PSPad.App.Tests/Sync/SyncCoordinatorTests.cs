using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Api;
using PSPad.App.Auth;
using PSPad.App.Tests.Auth;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;
using PSPad.App.Tests.State;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class SyncCoordinatorTests
{
    static readonly Guid User = Guid.NewGuid();

    readonly StatusBelts _belts = new();
    readonly InMemoryOutbox _rejectingOutbox = new();
    RejectingApi _rejecting = new();
    readonly FakeLanguageApi _languageApi = new();
    static readonly IStringLocalizer<ShellStrings> Text = TestLocalizers.For<ShellStrings>();
    readonly LanguagePreference _languages = new(new FakeStorageJsRuntime());
    readonly InMemoryLocalSessionStore _sessions = new(
        new LocalSession(User, "Ada", "ada@example.com", "UTC", "en", "refresh-token", DateTimeOffset.UtcNow));

    ServiceProvider? _provider;

    ServiceCollection Services { get; } = Arranged();

    IServiceProvider Provider => _provider ??= Services.BuildServiceProvider();

    NavigationManager Navigation => Provider.GetRequiredService<NavigationManager>();

    static ServiceCollection Arranged()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, SilentJsRuntime>();
        services.AddSingleton<NavigationManager, FakeNavigationManager>();
        return services;
    }

    [Fact]
    public async Task ARejectedChangeRaisesTheRejectedBeltAndKeepsItsMessage()
    {
        var coordinator = await CoordinatorRejecting("That list no longer exists.");

        await coordinator.SyncNowAsync();

        var belt = Assert.Single(_belts.Visible);
        Assert.Equal(BeltKind.Rejected, belt.Kind);
        Assert.Equal("1 change couldn’t be saved.", belt.Text);
        Assert.Equal("Details", belt.ActionText);
        Assert.Equal(["That list no longer exists."], coordinator.LastRejections);
    }

    [Theory]
    [InlineData(1, "1 change couldn’t be saved.")]
    [InlineData(2, "2 changes couldn’t be saved.")]
    public void TheBeltCountsTheRejectedChanges(int count, string text) =>
        Assert.Equal(text, CoordinatorFor(AnAreaCalled("Dom")).RejectedText(count));

    [Fact]
    public async Task DetailsOpensTheSyncCardInSettings()
    {
        var coordinator = await CoordinatorRejecting("That list no longer exists.");
        await coordinator.SyncNowAsync();

        await Assert.Single(_belts.Visible).Action!();

        Assert.EndsWith("/settings#sync", Navigation.Uri);
    }

    [Fact]
    public async Task TheSameRejectionComingBackStaysDismissed()
    {
        var coordinator = await CoordinatorRejecting("That list no longer exists.");
        await coordinator.SyncNowAsync();
        _belts.Dismiss(BeltKind.Rejected);

        await coordinator.SyncNowAsync();

        Assert.Empty(_belts.Visible);
    }

    [Fact]
    public async Task ANewlyRejectedCommandReopensADismissedBeltEvenWithTheSameReason()
    {
        var coordinator = await CoordinatorRejecting("That list no longer exists.");
        await coordinator.SyncNowAsync();
        _belts.Dismiss(BeltKind.Rejected);
        _rejecting.Reasons = [];
        await coordinator.SyncNowAsync();

        _rejecting.Reasons = ["That list no longer exists."];
        await QueueACommandAsync();
        await coordinator.SyncNowAsync();

        Assert.Equal(BeltKind.Rejected, Assert.Single(_belts.Visible).Kind);
    }

    [Fact]
    public async Task ACommandRejectedAgainWithANewReasonStaysDismissedButSettingsGetsTheReason()
    {
        var coordinator = await CoordinatorRejecting("First.");
        await coordinator.SyncNowAsync();
        _belts.Dismiss(BeltKind.Rejected);
        _rejecting.Reasons = ["Second."];

        await coordinator.SyncNowAsync();

        Assert.Empty(_belts.Visible);
        Assert.Equal(["Second."], coordinator.LastRejections);
    }

    [Fact]
    public async Task ACleanSyncKeepsTheLastRejectionsForSettings()
    {
        var coordinator = await CoordinatorRejecting("That list no longer exists.");
        await coordinator.SyncNowAsync();
        _rejecting.Reasons = [];

        await coordinator.SyncNowAsync();

        Assert.Equal(["That list no longer exists."], coordinator.LastRejections);
    }

    [Fact]
    public async Task NothingRejectedRaisesNoBelt()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();

        Assert.Empty(_belts.Visible);
        Assert.Empty(coordinator.LastRejections);
    }

    [Fact]
    public async Task ARejectionNoLongerPopsASnackbar()
    {
        var coordinator = await CoordinatorRejecting("That list no longer exists.");

        await coordinator.SyncNowAsync();

        Assert.Empty(Provider.GetRequiredService<ISnackbar>().ShownSnackbars);
    }

    async Task<SyncCoordinator> CoordinatorRejecting(params string[] reasons)
    {
        Services.AddMudServices();
        _rejecting = new RejectingApi { Reasons = reasons };
        await QueueACommandAsync();

        var replica = new InMemoryReplica();
        return new SyncCoordinator(
            new SyncService(_rejecting, replica, _rejectingOutbox), new FixedConnectivity(true), _rejectingOutbox,
            _belts, Navigation, replica, new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);
    }

    Task QueueACommandAsync() =>
        _rejectingOutbox.AppendAsync(
            Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));

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
        var coordinator = CoordinatorFor(new SyncResponse(7, new Dictionary<string, JsonElement[]>()));

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
    public void InvalidatingAdvancesTheRevisionAndAnnouncesIt()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"), online: false);
        var changes = 0;
        coordinator.Changed += () => changes++;

        coordinator.Invalidate();

        Assert.Equal(1, coordinator.Revision);
        Assert.Equal(1, changes);
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
    public async Task ABusyServerIsNotFlaggedAsAFailedSync()
    {
        var coordinator = CoordinatorFor(new BusyOnceApi(TimeSpan.FromHours(1)));

        await coordinator.SyncNowAsync();

        Assert.False(coordinator.LastSyncFailed);
    }

    [Fact]
    public async Task ABusyServerIsRetriedOnceItsRetryAfterHasPassed()
    {
        var api = new BusyOnceApi(TimeSpan.FromMilliseconds(10));
        var coordinator = CoordinatorFor(api);

        await coordinator.SyncNowAsync();
        await api.SecondCall.WaitAsync(TimeSpan.FromSeconds(5), global::Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(2, api.SyncCalls);
    }

    [Fact]
    public async Task ARejectionStillQueuedIsAStuckChange()
    {
        var (coordinator, _, _, _) = await StuckBehind(unrecoverable: false);

        await coordinator.SyncNowAsync();

        Assert.True(coordinator.HasStuckChange);
    }

    [Fact]
    public async Task ARejectionAlreadyDroppedIsNoStuckChange()
    {
        var (coordinator, _, _, _) = await StuckBehind(unrecoverable: true);

        await coordinator.SyncNowAsync();

        Assert.False(coordinator.HasStuckChange);
    }

    [Fact]
    public async Task DiscardingTheStuckChangeLetsTheRestOfTheQueueSync()
    {
        var (coordinator, outbox, api, _) = await StuckBehind(unrecoverable: false);
        await coordinator.SyncNowAsync();

        await coordinator.DiscardStuckChangeAsync();

        Assert.Equal(0, await outbox.CountAsync());
        Assert.Equal(2, api.Sent.Count(envelope => envelope.Type == "Behind"));
        Assert.False(coordinator.HasStuckChange);
    }

    [Fact]
    public async Task DiscardingTheStuckChangePullsEverythingAgainToUndoItLocally()
    {
        var (coordinator, _, api, replica) = await StuckBehind(unrecoverable: false);
        await coordinator.SyncNowAsync();
        await replica.SetMarkerAsync(40);

        await coordinator.DiscardStuckChangeAsync();

        Assert.Equal(0, api.LastSince);
    }

    async Task<(SyncCoordinator Coordinator, InMemoryOutbox Outbox, StuckApi Api, InMemoryReplica Replica)> StuckBehind(
        bool unrecoverable)
    {
        Services.AddMudServices();
        var outbox = new InMemoryOutbox();
        var replica = new InMemoryReplica();
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Stuck", JsonSerializer.SerializeToElement(new { })));
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Behind", JsonSerializer.SerializeToElement(new { })));
        var api = new StuckApi(unrecoverable);
        var coordinator = new SyncCoordinator(
            new SyncService(api, replica, outbox), new FixedConnectivity(true), outbox,
            _belts, Navigation, replica, new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);
        return (coordinator, outbox, api, replica);
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
            _belts, Navigation, replica, new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);
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
            _belts,
            Navigation,
            new InMemoryReplica(),
            new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);

        var first = coordinator.SyncNowAsync();
        var second = coordinator.SyncNowAsync();

        Assert.Same(first, second);
        Assert.Equal(1, api.SyncCalls);

        api.Release();
        await first;

        Assert.Equal(2, api.SyncCalls);
    }

    [Fact]
    public async Task APendingLanguageIsSentAfterASyncThatReachedTheServer()
    {
        await _languages.ChooseAsync("pl");
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();

        Assert.Equal(["pl"], _languageApi.Sent);
        Assert.False(_languages.HasPendingWrite);
    }

    [Fact]
    public async Task AConfirmedPendingLanguageIsRememberedInTheStoredSession()
    {
        await _languages.ChooseAsync("pl");
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();

        Assert.Equal("pl", _sessions.Current!.Language);
    }

    [Fact]
    public async Task AFailingLanguageRetryDoesNotHideRejectionsOrTheRevision()
    {
        await _languages.ChooseAsync("pl");
        _languageApi.Throws = true;
        var coordinator = await CoordinatorRejecting("That list no longer exists.");

        await coordinator.SyncNowAsync();

        Assert.True(_languages.HasPendingWrite);
        Assert.Equal(["That list no longer exists."], coordinator.LastRejections);
    }

    [Fact]
    public async Task AFailingLanguageRetryStillBumpsTheRevision()
    {
        await _languages.ChooseAsync("pl");
        _languageApi.Throws = true;
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();

        Assert.True(_languages.HasPendingWrite);
        Assert.Equal(1, coordinator.Revision);
    }

    [Fact]
    public async Task APendingLanguageStaysPendingWhenTheServerDoesNotConfirmIt()
    {
        await _languages.ChooseAsync("pl");
        _languageApi.Confirms = false;
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();

        Assert.True(_languages.HasPendingWrite);
    }

    [Fact]
    public async Task NothingIsSentWhenNoLanguageIsPending()
    {
        var coordinator = CoordinatorFor(AnAreaCalled("Dom"));

        await coordinator.SyncNowAsync();

        Assert.Empty(_languageApi.Sent);
    }

    sealed class FakeLanguageApi : ILanguageApi
    {
        public List<string> Sent { get; } = [];

        public bool Confirms { get; set; } = true;

        public bool Throws { get; set; }

        public Task<MeResponse?> SetLanguageAsync(string language)
        {
            Sent.Add(language);
            if (Throws)
            {
                throw new TaskCanceledException();
            }

            return Task.FromResult<MeResponse?>(
                Confirms ? new MeResponse(User, "Ada", "ada@example.com", "UTC", language) : null);
        }
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
            });
    }

    SyncCoordinator CoordinatorFor(HttpRequestException failure, InMemoryReplica? replica = null)
    {
        var local = replica ?? new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        Services.AddMudServices();
        return new SyncCoordinator(
            new SyncService(new FakeApi(null, failure), local, outbox), new FixedConnectivity(true), outbox,
            _belts, Navigation, local, new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);
    }

    SyncCoordinator CoordinatorFor(ISyncApi api)
    {
        Services.AddMudServices();
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        return new SyncCoordinator(
            new SyncService(api, replica, outbox), new FixedConnectivity(true), outbox,
            _belts, Navigation, replica, new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);
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
            _belts,
            Navigation,
            replica,
            new StoppedClock(Noon), _languages, _languageApi, _sessions, Text);
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

    sealed class BusyOnceApi(TimeSpan retryAfter) : ISyncApi
    {
        readonly TaskCompletionSource _secondCall = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SyncCalls { get; private set; }

        public Task SecondCall => _secondCall.Task;

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
        {
            SyncCalls++;
            if (SyncCalls == 1)
            {
                return Task.FromException<SyncResponse?>(new ServerBusyException(retryAfter));
            }

            _secondCall.TrySetResult();
            return Task.FromResult<SyncResponse?>(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), []));
        }

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    sealed class StuckApi(bool unrecoverable) : ISyncApi
    {
        public List<CommandEnvelope> Sent { get; } = [];

        public long? LastSince { get; private set; }

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            Sent.AddRange(envelopes);
            return Task.FromResult<IReadOnlyList<CommandResponse>>(
                [.. envelopes.Select(envelope => envelope.Type == "Stuck"
                    ? new CommandResponse(Guid.NewGuid(), false, "That budget does not exist.", unrecoverable)
                    : new CommandResponse(Guid.NewGuid(), true, null))]);
        }

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
        {
            LastSince = since;
            return Task.FromResult<SyncResponse?>(new SyncResponse(since, new Dictionary<string, JsonElement[]>(), []));
        }

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    sealed class RejectingApi : ISyncApi
    {
        public string[] Reasons { get; set; } = [];

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>(
                [.. envelopes.Select(_ => Reasons.Length == 0
                    ? new CommandResponse(Guid.NewGuid(), true, null)
                    : new CommandResponse(Guid.NewGuid(), false, Reasons[0]))]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
            Task.FromResult<SyncResponse?>(new SyncResponse(1, new Dictionary<string, JsonElement[]>()));

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
