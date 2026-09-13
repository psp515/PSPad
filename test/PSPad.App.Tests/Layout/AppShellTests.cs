using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AppShellTests : Bunit.TestContext
{
    [Fact]
    public void ItRendersOneSidebar()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Single(shell.FindComponents<NavSidebar>());
    }

    [Fact]
    public void TheHamburgerIsHiddenAtDesktopWidths()
    {
        Arrange();

        var shell = Render<AppShell>();

        Assert.Contains("d-md-none", shell.Find("#pspad-drawer-toggle").ClassList);
    }

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));

        var replica = new InMemoryReplica();
        var state = new AppState { UserId = Guid.NewGuid(), Today = new DateOnly(2026, 9, 12) };
        Services.AddSingleton<IReplica>(replica);
        Services.AddSingleton(state);
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
            new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
            state));
        Services.AddSingleton<IDocumentStore<Module.Tasks.Areas.Area>>(
            new ReplicaDocumentStore<Module.Tasks.Areas.Area>(replica));

        Services.AddSingleton(new PSPadApiClient(new HttpClient()));
        Services.AddSingleton<ISyncApi>(sp => sp.GetRequiredService<PSPadApiClient>());
        Services.AddSingleton<IOutbox>(new InMemoryOutbox());
        Services.AddSingleton<IConnectivity>(new FakeConnectivity());
        Services.AddScoped<SyncService>();
        Services.AddScoped<SyncCoordinator>();
        Services.AddScoped<ReplicaUnitOfWork>();
        Services.AddScoped<CommandSender>();
    }

    sealed class FakeConnectivity : IConnectivity
    {
        public bool IsOnline => false;

#pragma warning disable CS0067
        public event Action? CameOnline;
#pragma warning restore CS0067
    }
}
