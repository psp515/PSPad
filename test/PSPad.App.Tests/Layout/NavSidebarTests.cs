using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.Tests;
using PSPad.App.Theme;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class NavSidebarTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Theory]
    [InlineData("My Day")]
    [InlineData("Inbox")]
    [InlineData("Goals")]
    [InlineData("History")]
    [InlineData("New area")]
    public void ItCarriesEverySection(string text)
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Contains(text, sidebar.Markup);
    }

    [Fact]
    public void ItListsEveryArea()
    {
        Arrange();

        var sidebar = Render(Areas("Dom", "Praca", "Studia", "Ogród", "Wesele", "Magazyn"));

        foreach (var name in new[] { "Dom", "Praca", "Studia", "Ogród", "Wesele", "Magazyn" })
        {
            Assert.Contains(name, sidebar.Markup);
        }
    }

    [Fact]
    public void AreasLoadingIsMarkedAsALiveLoadingRegion()
    {
        Arrange();

        var sidebar = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, Array.Empty<Area>())
            .Add(p => p.AreasLoaded, false)
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User));

        var status = sidebar.Find("[role='status']");
        Assert.Equal("true", status.GetAttribute("aria-busy"));
    }

    [Fact]
    public void ItOrdersAreasByPositionNotInsertionOrder()
    {
        Arrange();

        var sidebar = Render(Areas(("Praca", 2), ("Dom", 0), ("Studia", 1)));

        var markup = sidebar.Markup;
        Assert.True(markup.IndexOf("Dom") < markup.IndexOf("Studia"));
        Assert.True(markup.IndexOf("Studia") < markup.IndexOf("Praca"));
    }

    [Fact]
    public void EveryAreaLinksToItsOwnScreen()
    {
        Arrange();
        var areas = Areas("Dom", "Praca");

        var sidebar = Render(areas);

        foreach (var area in areas)
        {
            Assert.Contains($"/areas/{area.Id}", sidebar.Markup);
        }
    }

    [Fact]
    public void GoalsAndHistoryAreBothSidebarRows()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Contains("/goals\"", sidebar.Markup);
        Assert.Contains("/history\"", sidebar.Markup);
    }

    [Fact]
    public void NewAreaIsThereEvenWithNoAreasAtAll()
    {
        Arrange();

        var sidebar = Render([]);

        Assert.Contains("New area", sidebar.Markup);
    }

    [Fact]
    public void ClickingAnAreaRowRaisesNavigated()
    {
        Arrange();
        var area = Areas("Dom")[0];
        var navigated = 0;

        var sidebar = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, new[] { area })
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User)
            .Add(p => p.Navigated, EventCallback.Factory.Create(this, () => navigated++)));

        sidebar.Find($"a[href='/areas/{area.Id}']").Click();

        Assert.Equal(1, navigated);
    }

    [Fact]
    public void ClickingNewAreaRaisesOnNewArea()
    {
        Arrange();
        var newArea = 0;

        var sidebar = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, Areas("Dom"))
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User)
            .Add(p => p.OnNewArea, EventCallback.Factory.Create(this, () => newArea++)));

        sidebar.Find(".pspad-new-area").Click();

        Assert.Equal(1, newArea);
    }

    [Fact]
    public void ClickingNewAreaWhileDisabledDoesNotRaiseOnNewArea()
    {
        Arrange();
        var newArea = 0;

        var sidebar = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, Areas("Dom"))
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User)
            .Add(p => p.Disabled, true)
            .Add(p => p.OnNewArea, EventCallback.Factory.Create(this, () => newArea++)));

        sidebar.Find(".pspad-new-area").Click();

        Assert.Equal(0, newArea);
    }

    [Fact]
    public void RenamingWhileDisabledDoesNotRaiseOnRenameArea()
    {
        Arrange();
        var area = Areas("Dom")[0];
        Area? renamed = null;

        var sidebar = Render(BuildSidebarWithPopover(area, a => renamed = a, disabled: true));

        sidebar.Find(".pspad-area-menu button").Click();
        sidebar.FindAll(".mud-menu-item")[0].Click();

        Assert.Null(renamed);
    }

    [Fact]
    public void EveryAreaRowCarriesAMenu()
    {
        Arrange();

        var sidebar = Render(Areas("Dom", "Praca"));

        Assert.Equal(2, sidebar.FindComponents<ThingMenu>().Count);
    }

    [Fact]
    public void RenamingAnAreaRaisesItWithTheArea()
    {
        Arrange();
        var area = Areas("Dom")[0];
        Area? renamed = null;

        var sidebar = Render(BuildSidebarWithPopover(area, a => renamed = a, disabled: false));

        sidebar.Find(".pspad-area-menu button").Click();
        sidebar.FindAll(".mud-menu-item")[0].Click();

        Assert.Equal(area.Id, renamed?.Id);
    }

    // MudMenu portals its open content through MudPopoverProvider, so this render
    // tree needs one alongside NavSidebar for the menu item click to be reachable.
    RenderFragment BuildSidebarWithPopover(Area area, Action<Area> onRenameArea, bool disabled) => builder =>
    {
        builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<NavSidebar>(1);
        builder.AddAttribute(2, nameof(NavSidebar.Areas), new[] { area });
        builder.AddAttribute(3, nameof(NavSidebar.Email), "ada@example.com");
        builder.AddAttribute(4, nameof(NavSidebar.UserId), User);
        builder.AddAttribute(5, nameof(NavSidebar.OnRenameArea),
            EventCallback.Factory.Create(this, onRenameArea));
        builder.AddAttribute(6, nameof(NavSidebar.Disabled), disabled);
        builder.CloseComponent();
    };

    IRenderedComponent<NavSidebar> Render(IReadOnlyList<Area> areas) =>
        Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, areas)
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User));

    void Arrange()
    {
        var today = new DateOnly(2026, 9, 12);
        var replica = AppTestHost.Arrange(this, User, today);
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
            new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
            new AppState { UserId = User, Today = today }));
    }

    static Area[] Areas(params string[] names) =>
        Areas([.. names.Select((name, index) => (name, index))]);

    static Area[] Areas(params (string Name, int Position)[] entries) =>
        [.. entries.Select(entry =>
        {
            var area = new Area();
            area.ApplyAll(Area.Decide(
                null,
                new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), entry.Name, entry.Position),
                DateTimeOffset.UnixEpoch));
            return area;
        })];
}
