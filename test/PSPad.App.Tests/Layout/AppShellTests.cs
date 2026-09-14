using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.Sync;
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
    public void ThePersistentDrawerIsAlwaysOpenAndDesktopOnly()
    {
        Arrange();

        var shell = Render<AppShell>();

        var persistent = shell.FindComponents<MudDrawer>()
            .Single(drawer => drawer.Instance.Variant == DrawerVariant.Persistent);
        Assert.Contains("mud-drawer--open", persistent.Find(".mud-drawer").ClassList);
        Assert.Contains("d-none", persistent.Find(".mud-drawer").ClassList);
        Assert.Contains("d-md-flex", persistent.Find(".mud-drawer").ClassList);
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
        Arrange(kept, removed);
        var authStateTask = AuthenticatedAs("Kolber", "kolberu@gmail.com");

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

    void Arrange(params Aggregate[] documents)
    {
        var today = new DateOnly(2026, 9, 12);
        var replica = AppTestHost.Arrange(this, User, today, documents);
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
            new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
            new AppState { UserId = User, Today = today }));

        var meResponse = new MeResponse(User, "Kolber", "UTC");
        Services.AddSingleton(new PSPadApiClient(new HttpClient(new FakeMeHandler(meResponse))
        {
            BaseAddress = new Uri("http://localhost/")
        }));
        Services.AddSingleton<ISyncApi>(sp => sp.GetRequiredService<PSPadApiClient>());
        Services.AddSingleton<IConnectivity>(new FakeConnectivity());
        Services.AddScoped<SyncService>();
        Services.AddScoped<SyncCoordinator>();
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

    static Task<AuthenticationState> AuthenticatedAs(string name, string email)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), new Claim("email", email)], "test");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    sealed class FakeConnectivity : IConnectivity
    {
        public bool IsOnline => false;

#pragma warning disable CS0067
        public event Action? CameOnline;
#pragma warning restore CS0067
    }

    sealed class FakeMeHandler(MeResponse response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
    }
}
