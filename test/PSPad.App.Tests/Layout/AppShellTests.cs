using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Auth;
using PSPad.App.Components;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.App.State.Viewport;
using PSPad.App.Sync;
using PSPad.App.Tests.Auth;
using PSPad.App.Theme;
using PSPad.App.Updates;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AppShellTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void OnTheDesktopThePageInsetIsPaddingSoNoMarginCollapsesAboveTheShell()
    {
        Arrange();

        var shell = Render<AppShell>();

        var content = shell.Find(".pspad-content").ClassList;
        Assert.Contains("mt-md-0", content);
        Assert.Contains("pt-md-6", content);
        Assert.DoesNotContain("mt-md-2", content);
        Assert.DoesNotContain("my-4", content);
    }

    [Fact]
    public void OnlyTheDesktopCarriesTheSidebar()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Single(shell.FindComponents<NavSidebar>());
    }

    [Fact]
    public void ThePersistentDrawerIsOpenOnDesktopAndDesktopOnly()
    {
        Arrange();

        var shell = Render<AppShell>();

        var persistent = shell.FindComponents<MudDrawer>()
            .Single(drawer => drawer.Instance.Variant == DrawerVariant.Persistent);
        Assert.Contains("mud-drawer--open", persistent.Find(".mud-drawer").ClassList);
        Assert.Contains("d-none", persistent.Find(".mud-drawer").ClassList);
        Assert.Contains("d-md-flex", persistent.Find(".mud-drawer").ClassList);
    }

    // MudBlazor pushes .mud-main-content over by the persistent drawer's width whenever it
    // is logically Open, regardless of the CSS classes that hide it on a small viewport -- so
    // Open must track the breakpoint, or content stays shoved aside on mobile forever.
    [Fact]
    public void ThePersistentDrawerIsClosedBelowTheDesktopBreakpointSoItStopsReservingSpace()
    {
        Arrange();
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var shell = Render<AppShell>();

        var persistent = shell.FindComponents<MudDrawer>()
            .Single(drawer => drawer.Instance.Variant == DrawerVariant.Persistent);
        Assert.DoesNotContain("mud-drawer--open", persistent.Find(".mud-drawer").ClassList);
    }

    [Fact]
    public void ThereIsNoHamburgerAnymore()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Empty(shell.FindAll("#pspad-drawer-toggle"));
    }

    [Fact]
    public void PhonesGetTheTopBarBottomBarAndAccountDrawer()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Single(shell.FindComponents<MobileTopBar>());
        Assert.Single(shell.FindComponents<BottomNav>());
        Assert.Single(shell.FindComponents<AccountDrawer>());
    }

    [Fact]
    public void TheAvatarOpensTheAccountDrawer()
    {
        Arrange();
        var shell = Render<AppShell>();

        shell.Find(".pspad-avatar-button").Click();

        Assert.True(shell.FindComponent<AccountDrawer>().Instance.Open);
    }

    [Fact]
    public async Task CrossingToDesktopClosesTheAccountDrawer()
    {
        Arrange();
        var viewport = new AppTestHost.FakeViewport(isDesktop: false);
        Services.AddSingleton<IViewport>(viewport);
        var shell = Render<AppShell>();
        shell.Find(".pspad-avatar-button").Click();
        Assert.True(shell.FindComponent<AccountDrawer>().Instance.Open);

        await shell.InvokeAsync(() => viewport.ChangeTo(true));

        shell.WaitForAssertion(() => Assert.False(shell.FindComponent<AccountDrawer>().Instance.Open));
    }

    [Fact]
    public void NavigatingClosesTheAccountDrawer()
    {
        Arrange();
        var shell = Render<AppShell>();
        shell.Find(".pspad-avatar-button").Click();

        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("settings");

        shell.WaitForAssertion(() => Assert.False(shell.FindComponent<AccountDrawer>().Instance.Open));
    }

    [Fact]
    public void TheAccountDrawerCarriesTheSignedInIdentity()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");

        var shell = Render<AppShell>();

        shell.WaitForAssertion(() =>
        {
            var drawer = shell.FindComponent<AccountDrawer>().Instance;
            Assert.Equal("ada@example.com", drawer.Email);
            Assert.Equal("Ada Lovelace", drawer.DisplayName);
        });
    }

    [Fact]
    public void TheNewAreaButtonIsWiredToAHandler()
    {
        Arrange();

        var shell = Render<AppShell>();
        var sidebars = shell.FindComponents<NavSidebar>();

        Assert.All(sidebars, sidebar =>
            Assert.True(sidebar.Instance.OnNewArea.HasDelegate));
    }

    [Fact]
    public void DeletedAreasAreNotInTheSidebar()
    {
        var kept = NewArea("Dom", 0);
        var removed = NewArea("Stare", 1);
        removed.ApplyAll(Area.Decide(
            removed, new DeleteArea(Guid.NewGuid(), User, removed.Id), DateTimeOffset.UnixEpoch));
        Arrange(documents: [kept, removed]);
        var authStateTask = AuthenticatedAs("Ada Lovelace", "ada@example.com");

        var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(authStateTask));

        Assert.Contains("Dom", shell.Markup);
        Assert.DoesNotContain("Stare", shell.Markup);
    }

    [Fact]
    public void OpeningTheAppOnAUrlWithATaskQueryRendersThePanelForIt()
    {
        Arrange();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"lists/{listId}?task={taskId}");

        var shell = Render<AppShell>();

        Assert.Equal(taskId, shell.FindComponent<TaskDetailPanel>().Instance.TaskId);
    }

    [Fact]
    public void ANewTaskQueryOpensTheTaskPanelToAddIntoThatList()
    {
        Arrange();
        var listId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"lists/{listId}?task=new&list={listId}");

        var shell = Render<AppShell>();

        var panel = shell.FindComponent<TaskDetailPanel>().Instance;
        Assert.Null(panel.TaskId);
        Assert.Equal(listId, panel.NewInList);
    }

    [Fact]
    public void OpeningTheAppOnAUrlWithAnItemQueryRendersTheReferenceItemPanelForIt()
    {
        Arrange();
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"lists/{listId}?item={itemId}");

        var shell = Render<AppShell>();

        Assert.Equal(itemId, shell.FindComponent<ReferenceItemPanel>().Instance.ItemId);
    }

    [Fact]
    public void ANewItemQueryOpensTheReferenceItemPanelToAddIntoThatList()
    {
        Arrange();
        var listId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"lists/{listId}?item=new&list={listId}");

        var shell = Render<AppShell>();

        var panel = shell.FindComponent<ReferenceItemPanel>().Instance;
        Assert.Null(panel.ItemId);
        Assert.Equal(listId, panel.NewInList);
    }

    [Fact]
    public void ClosingTheReferenceItemPanelDropsTheQuery()
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Przepisy", ListKind.Reference),
            DateTimeOffset.UnixEpoch));
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "Bigos", 0),
            DateTimeOffset.UnixEpoch));
        Arrange(documents: [list, item]);
        var screen = $"lists/{list.Id}";
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"{screen}?item={item.Id}");
        var shell = Render<AppShell>();

        shell.Find(".pspad-panel-close").Click();

        shell.WaitForAssertion(() =>
        {
            Assert.Null(shell.FindComponent<ReferenceItemPanel>().Instance.ItemId);
            Assert.DoesNotContain("item=", navigation.Uri);
        });
    }

    [Fact]
    public void ANewListQueryOpensTheListPanelToAddIntoThatArea()
    {
        Arrange();
        var areaId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"areas/{areaId}?list=new&inarea={areaId}");

        var shell = Render<AppShell>();

        Assert.Equal(areaId, shell.FindComponent<ListDetailPanel>().Instance.NewInArea);
        Assert.Null(shell.FindComponent<AreaDetailPanel>().Instance.AreaId);
    }

    [Fact]
    public void PageContentUsesTheFullWidth()
    {
        Arrange();

        var shell = Render<AppShell>();

        var content = shell.Find(".pspad-content");
        Assert.DoesNotContain("mud-container-maxwidth-lg", content.ClassName);
        Assert.Contains("mud-container-maxwidth-false", content.ClassName);
    }

    [Fact]
    public void ItTakesTheEmailFromApiMeRatherThanTheClaim()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com", emailClaim: null);

        var shell = Render<AppShell>();

        var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
        Assert.Equal("ada@example.com", sidebar.Email);
        Assert.Equal("Ada Lovelace", sidebar.DisplayName);
    }

    [Fact]
    public void ItRendersSafelyForAnUnauthenticatedUser()
    {
        Arrange(hasSession: false);

        var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal()))));

        Assert.Empty(shell.FindComponents<MudProgressCircular>());
        var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
        Assert.Equal("", sidebar.Email);
        Assert.Equal("", sidebar.DisplayName);
    }

    [Fact]
    public void ANullDisplayNameOrEmailFromApiMeDoesNotCrashTheShell()
    {
        Arrange(meResponseOverride: new MeResponse(User, null!, null!, "UTC"));
        var authStateTask = AuthenticatedAs("Ada Lovelace", "ada@example.com");

        var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(authStateTask));

        shell.WaitForAssertion(() =>
        {
            var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
            Assert.Equal(User, sidebar.UserId);
            Assert.Equal("", sidebar.DisplayName);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ANullTimeZoneFromApiMeDoesNotCrashTheShell()
    {
        Arrange(meResponseOverride: new MeResponse(User, "Ada Lovelace", "ada@example.com", null!));
        var authStateTask = AuthenticatedAs("Ada Lovelace", "ada@example.com");

        var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(authStateTask));

        shell.WaitForAssertion(() =>
        {
            var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
            Assert.Equal(User, sidebar.UserId);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GoingBackDropsTheTaskButLeavesTheShellOnTheSameScreen()
    {
        Arrange();
        var screen = $"lists/{Guid.NewGuid()}";
        var taskId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo($"{screen}?task={taskId}");
        var shell = Render<AppShell>();

        navigation.NavigateTo(screen);

        shell.WaitForAssertion(() =>
        {
            Assert.Null(shell.FindComponent<TaskDetailPanel>().Instance.TaskId);
            Assert.EndsWith(screen, navigation.Uri);
        });
    }

    [Fact]
    public void ItBecomesReadyOfflineWhenTheAccountFetchFails()
    {
        Arrange(accountFetchFails: true);

        var shell = Render<AppShell>();

        Assert.Empty(shell.FindComponents<BrandLoader>());
    }

    [Fact]
    public void ItTakesIdentityFromTheSessionWithoutTheServer()
    {
        Arrange(accountFetchFails: true);

        var shell = Render<AppShell>();

        Assert.Contains("Zoe", shell.Markup);
    }

    [Fact]
    public void TheSyncCoordinatorStartsEvenWhenTheAccountFetchFails()
    {
        Arrange(accountFetchFails: true);
        var connectivity = (SpyConnectivity)Services.GetRequiredService<IConnectivity>();

        Render<AppShell>();

        Assert.True(connectivity.IsOnlineReads > 0);
    }

    [Fact]
    public void AThrowingSessionStoreLeavesTheShellReadyAndAnonymousRatherThanThrowing()
    {
        Arrange();
        Services.AddSingleton<ILocalSessionStore>(new ThrowingLocalSessionStore());

        var shell = Render<AppShell>();

        Assert.Empty(shell.FindComponents<BrandLoader>());
        var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
        Assert.Equal("", sidebar.DisplayName);
        Assert.Equal("", sidebar.Email);
    }

    [Fact]
    public void ItBecomesReadyWhenLoadingTheLocalReplicaFails()
    {
        Arrange();
        Services.AddSingleton<IDocumentStore<Area>>(new ThrowingAreaStore());

        var shell = Render<AppShell>();

        Assert.Empty(shell.FindComponents<BrandLoader>());
    }

    [Fact]
    public void ItKeepsTheTokenAndContactTimeWrittenWhileTheAccountFetchWasInFlight()
    {
        var signedIn = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
        var refreshed = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);
        var store = new InMemoryLocalSessionStore(
            new LocalSession(User, "Zoe Session", "zoe@example.com", "UTC", "r0", signedIn));
        Arrange(
            sessionStore: store,
            onMeRequest: () => store.SaveAsync(
                store.Current! with { RefreshToken = "r1", LastServerContactUtc = refreshed }));

        Render<AppShell>();

        Assert.Equal("r1", store.Current?.RefreshToken);
        Assert.Equal(refreshed, store.Current?.LastServerContactUtc);
    }

    [Fact]
    public void ItTakesTodayFromTheRegisteredClockAndTheSessionTimeZone()
    {
        var store = new InMemoryLocalSessionStore(new LocalSession(
            User, "Zoe Session", "zoe@example.com", "Etc/GMT+12", "refresh-token", DateTimeOffset.UtcNow));
        Arrange(
            sessionStore: store,
            meResponseOverride: new MeResponse(User, "Ada Lovelace", "ada@example.com", "Etc/GMT+12"));

        Render<AppShell>();

        Assert.Equal(new DateOnly(2026, 9, 11), Services.GetRequiredService<AppState>().Today);
    }

    [Fact]
    public void ItStaysReadyOnATimeZoneTheRuntimeDoesNotCarry()
    {
        var store = new InMemoryLocalSessionStore(new LocalSession(
            User, "Zoe Session", "zoe@example.com", "Mars/Olympus", "refresh-token", DateTimeOffset.UtcNow));
        Arrange(
            sessionStore: store,
            meResponseOverride: new MeResponse(User, "Ada Lovelace", "ada@example.com", "Mars/Olympus"));

        var shell = Render<AppShell>();

        Assert.Empty(shell.FindComponents<BrandLoader>());
        Assert.Equal(new DateOnly(2026, 9, 12), Services.GetRequiredService<AppState>().Today);
    }

    [Fact]
    public void ASignedInShellTearsTheBootSplashDownOnceItIsReady()
    {
        Arrange();

        var shell = Render<AppShell>();

        shell.WaitForAssertion(() => JSInterop.VerifyInvoke("pspadBoot.done"));
    }

    [Fact]
    public void TheBootSplashStaysUpWhileTheFirstPullIsInFlight()
    {
        // Tearing it down early shows the bare shell chrome around a loader, then the app.
        Arrange();
        var pull = new TaskCompletionSource<SyncResponse?>();
        Services.AddSingleton<ISyncApi>(new GatedSyncApi(pull.Task));
        Services.AddSingleton<IConnectivity>(new AlwaysOnline());

        var shell = Render<AppShell>();

        Assert.Empty(JSInterop.Invocations["pspadBoot.done"]);

        pull.SetResult(new SyncResponse(9, new Dictionary<string, System.Text.Json.JsonElement[]>(), []));

        shell.WaitForAssertion(() => JSInterop.VerifyInvoke("pspadBoot.done"));
    }

    [Fact]
    public async Task AServerThatNeverAnswersTheAccountFetchDoesNotHoldTheAppBack()
    {
        // A shop's captive network accepts the request and never replies.
        Arrange(accountFetchHangs: true);
        Services.AddSingleton<IViewport>(new YieldingViewport());
        await Services.GetRequiredService<IReplica>().SetMarkerAsync(12);

        var shell = Render<AppShell>();

        shell.WaitForAssertion(() => Assert.Empty(shell.FindComponents<BrandLoader>()));
        JSInterop.VerifyInvoke("pspadBoot.done");
    }

    [Fact]
    public void ASignedOutShellLeavesTheBootSplashToTheSignInScreen()
    {
        // The shell only hosts the redirect to /welcome here; releasing would flash its chrome.
        Arrange(hasSession: false);

        Render<AppShell>();

        Assert.Empty(JSInterop.Invocations["pspadBoot.done"]);
    }

    [Fact]
    public void ASignInWithNothingStoredLocallyWaitsForTheFirstPullBeforeShowingTheApp()
    {
        // Signing in wipes the replica. Rendering the app from it while the pull is still in
        // flight shows the user an empty account and calls it loaded.
        Arrange();
        var pull = new TaskCompletionSource<SyncResponse?>();
        Services.AddSingleton<ISyncApi>(new GatedSyncApi(pull.Task));
        Services.AddSingleton<IConnectivity>(new AlwaysOnline());

        var shell = Render<AppShell>();

        Assert.NotEmpty(shell.FindComponents<BrandLoader>());

        pull.SetResult(new SyncResponse(9, new Dictionary<string, System.Text.Json.JsonElement[]>(), []));

        shell.WaitForAssertion(() => Assert.Empty(shell.FindComponents<BrandLoader>()));
    }

    [Fact]
    public async Task ADeviceThatHasPulledBeforeShowsItsDataWithoutWaitingForTheServer()
    {
        // The whole point of the local replica: a relaunch opens on stored data, server or no server.
        Arrange();
        Services.AddSingleton<ISyncApi>(new GatedSyncApi(new TaskCompletionSource<SyncResponse?>().Task));
        Services.AddSingleton<IConnectivity>(new AlwaysOnline());
        await Services.GetRequiredService<IReplica>().SetMarkerAsync(12);

        var shell = Render<AppShell>();

        Assert.Empty(shell.FindComponents<BrandLoader>());
    }

    [Fact]
    public async Task APanelInTheUrlOpensWhileTheAccountFetchHangs()
    {
        Arrange(accountFetchHangs: true);
        await Services.GetRequiredService<IReplica>().SetMarkerAsync(12);
        var taskId = Guid.NewGuid();
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo($"today?task={taskId}");

        var shell = Render<AppShell>();

        shell.WaitForAssertion(() =>
            Assert.Equal(taskId, shell.FindComponent<TaskDetailPanel>().Instance.TaskId));
    }

    [Fact]
    public async Task ASignInAfterTheAnonymousRedirectStillWaitsForItsOwnFirstPull()
    {
        // AuthorizeRouteView renders the signed-out redirect inside this shell, so by the time
        // the signed-in shell mounts the coordinator has already run -- and finished -- a pull
        // that had no session behind it.
        var store = new InMemoryLocalSessionStore(null);
        Arrange(sessionStore: store);
        var pull = new TaskCompletionSource<SyncResponse?>();
        Services.AddSingleton<ISyncApi>(new QueuedSyncApi(
            Task.FromResult<SyncResponse?>(null), pull.Task));
        Services.AddSingleton<IConnectivity>(new AlwaysOnline());

        Render<AppShell>();
        await DisposeComponentsAsync();

        var session = new LocalSession(
            User, "Zoe Session", "zoe@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow);
        await store.SaveAsync(session);
        Services.GetRequiredService<LocalAuthenticationStateProvider>().SignedIn(session);
        var shell = Render<AppShell>();

        Assert.NotEmpty(shell.FindComponents<BrandLoader>());

        pull.SetResult(new SyncResponse(9, new Dictionary<string, System.Text.Json.JsonElement[]>(), []));

        shell.WaitForAssertion(() => Assert.Empty(shell.FindComponents<BrandLoader>()));
    }

    sealed class QueuedSyncApi(params Task<SyncResponse?>[] pulls) : ISyncApi
    {
        int _call;

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
            pulls[Math.Min(_call++, pulls.Length - 1)];

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    sealed class AlwaysOnline : IConnectivity
    {
        public bool IsOnline => true;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }

    sealed class GatedSyncApi(Task<SyncResponse?> pull) : ISyncApi
    {
        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) => pull;

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    void Arrange(
        string displayName = "Ada Lovelace",
        string email = "ada@example.com",
        string? emailClaim = "ada@example.com",
        bool accountFetchFails = false,
        bool accountFetchHangs = false,
        bool hasSession = true,
        MeResponse? meResponseOverride = null,
        InMemoryLocalSessionStore? sessionStore = null,
        Action? onMeRequest = null,
        params Aggregate[] documents)
    {
        var today = new DateOnly(2026, 9, 12);
        var replica = AppTestHost.Arrange(this, User, today, documents);
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
            new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
            new AppState { UserId = User, Today = today }));

        Services.AddSingleton<ILocalSessionStore>(sessionStore ?? new InMemoryLocalSessionStore(
            hasSession
                ? new LocalSession(
                    User, "Zoe Session", "zoe@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow)
                : null));
        Services.AddSingleton<LocalAuthenticationStateProvider>();

        var meResponse = meResponseOverride ?? new MeResponse(User, displayName, email, "UTC");
        HttpMessageHandler handler = accountFetchFails
            ? new ThrowingMeHandler()
            : accountFetchHangs
                ? new HangingMeHandler()
                : new FakeMeHandler(meResponse, onMeRequest);

        Services.AddSingleton(new PSPadApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        Services.AddSingleton<ISyncApi>(sp => sp.GetRequiredService<PSPadApiClient>());
        Services.AddSingleton<IConnectivity>(new SpyConnectivity());
        Services.AddScoped<SyncService>();
        Services.AddScoped<SyncCoordinator>();

        var authStateTask = AuthenticatedAs(displayName, emailClaim);
        RenderTree.Add<CascadingValue<Task<AuthenticationState>>>(parameters =>
            parameters.Add(cascade => cascade.Value, authStateTask));
    }

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null,
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position),
            DateTimeOffset.UnixEpoch));
        return area;
    }

    static Task<AuthenticationState> AuthenticatedAs(string name, string? emailClaim)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, name) };
        if (emailClaim is not null)
        {
            claims.Add(new Claim("email", emailClaim));
        }

        var identity = new ClaimsIdentity(claims, "test");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    [Fact]
    public void LosingTheServerRaisesTheOfflineBeltOnceAndNoSnackbar()
    {
        Arrange();
        var reachability = Services.GetRequiredService<ServerReachability>();
        var belts = Services.GetRequiredService<StatusBelts>();
        var shell = Render<AppShell>();

        shell.InvokeAsync(() => reachability.Failed(browserIsOnline: true));
        shell.InvokeAsync(() => reachability.Failed(browserIsOnline: true));

        var belt = Assert.Single(belts.Visible);
        Assert.Equal(BeltKind.Offline, belt.Kind);
        Assert.Equal(
            "Can’t reach the server — working from local data. Changes sync when you’re back.", belt.Text);
        Assert.Equal("Retry", belt.ActionText);
        Assert.Empty(Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        shell.WaitForAssertion(() => Assert.Contains("Can’t reach the server", shell.Find(".pspad-belt").TextContent));
    }

    [Fact]
    public void RetryRunsASync()
    {
        Arrange();
        var reachability = Services.GetRequiredService<ServerReachability>();
        var connectivity = (SpyConnectivity)Services.GetRequiredService<IConnectivity>();
        var shell = Render<AppShell>();
        shell.InvokeAsync(() => reachability.Failed(browserIsOnline: true));
        var readsBefore = connectivity.IsOnlineReads;

        shell.WaitForElement(".pspad-belt-action").Click();

        Assert.True(connectivity.IsOnlineReads > readsBefore);
    }

    [Fact]
    public void TheServerAnsweringAgainTakesTheOfflineBeltAway()
    {
        Arrange();
        var reachability = Services.GetRequiredService<ServerReachability>();
        var belts = Services.GetRequiredService<StatusBelts>();
        var shell = Render<AppShell>();
        shell.InvokeAsync(() => reachability.Failed(browserIsOnline: true));

        shell.InvokeAsync(reachability.Succeeded);

        Assert.Empty(belts.Visible);
        shell.WaitForAssertion(() => Assert.Empty(shell.FindAll(".pspad-belt")));
    }

    [Fact]
    public void TheBeltsSitAboveThePageInFlow()
    {
        Arrange();
        var shell = Render<AppShell>();

        shell.InvokeAsync(() => Services.GetRequiredService<ServerReachability>().Failed(browserIsOnline: true));

        shell.WaitForAssertion(() => Assert.NotNull(shell.Find(".mud-main-content > .pspad-belts + .pspad-content")));
    }

    [Fact]
    public async Task ANewVersionOffersAReloadBeltThatActivatesIt()
    {
        Arrange();
        var updates = (AppTestHost.FakeAppUpdates)Services.GetRequiredService<IAppUpdates>();
        var belts = Services.GetRequiredService<StatusBelts>();
        var shell = Render<AppShell>();

        await shell.InvokeAsync(updates.Announce);

        var belt = Assert.Single(belts.Visible);
        Assert.Equal(BeltKind.Update, belt.Kind);
        Assert.Equal("A new version of PSPad is ready.", belt.Text);
        Assert.Empty(Services.GetRequiredService<ISnackbar>().ShownSnackbars);

        var reload = shell.WaitForElement(".pspad-belt-action");
        Assert.Equal("Reload", reload.TextContent.Trim());
        reload.Click();

        Assert.Equal(1, updates.Applied);
    }

    [Fact]
    public async Task AVersionAlreadyWaitingWhenTheShellMountsIsOfferedOnce()
    {
        Arrange();
        var updates = (AppTestHost.FakeAppUpdates)Services.GetRequiredService<IAppUpdates>();
        updates.Announce();
        var belts = Services.GetRequiredService<StatusBelts>();

        Render<AppShell>();
        await DisposeComponentsAsync();
        Render<AppShell>();

        Assert.Equal(BeltKind.Update, Assert.Single(belts.Visible).Kind);
    }

    sealed class SpyConnectivity : IConnectivity
    {
        public int IsOnlineReads { get; private set; }

        public bool IsOnline
        {
            get
            {
                IsOnlineReads++;
                return false;
            }
        }

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }

    sealed class FakeMeHandler(MeResponse response, Action? onRequest = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            onRequest?.Invoke();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
        }
    }

    sealed class ThrowingMeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"));
    }

    sealed class YieldingViewport : IViewport
    {
        public async Task SubscribeAsync(Action<bool> onDesktopChanged)
        {
            await Task.Yield();
            onDesktopChanged(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class HangingMeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            new TaskCompletionSource<HttpResponseMessage>().Task;
    }

    sealed class ThrowingAreaStore : IDocumentStore<Area>
    {
        public Task<Area?> LoadAsync(Guid id, CancellationToken ct) =>
            throw new InvalidOperationException("replica unreadable");

        public Task<IReadOnlyList<Area>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            throw new InvalidOperationException("replica unreadable");
    }
}
