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
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AppShellTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItRendersOneSidebarPerBreakpointBranch()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Equal(2, shell.FindComponents<NavSidebar>().Count);
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
    public void TheTemporaryDrawerIsMobileOnly()
    {
        Arrange();

        var shell = Render<AppShell>();

        var temporary = shell.FindComponents<MudDrawer>()
            .Single(drawer => drawer.Instance.Variant == DrawerVariant.Temporary
                && drawer.FindComponents<NavSidebar>().Count > 0);
        Assert.Contains("d-md-none", temporary.Find(".mud-drawer").ClassList);
    }

    [Fact]
    public void TheHamburgerCarriesTheMobileOnlyClass()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Contains("d-md-none", shell.Find("#pspad-drawer-toggle").ClassList);
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

    void Arrange(
        string displayName = "Ada Lovelace",
        string email = "ada@example.com",
        string? emailClaim = "ada@example.com",
        bool accountFetchFails = false,
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

        var meResponse = meResponseOverride ?? new MeResponse(User, displayName, email, "UTC");
        HttpMessageHandler handler = accountFetchFails
            ? new ThrowingMeHandler()
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

    sealed class ThrowingAreaStore : IDocumentStore<Area>
    {
        public Task<Area?> LoadAsync(Guid id, CancellationToken ct) =>
            throw new InvalidOperationException("replica unreadable");

        public Task<IReadOnlyList<Area>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            throw new InvalidOperationException("replica unreadable");
    }
}
