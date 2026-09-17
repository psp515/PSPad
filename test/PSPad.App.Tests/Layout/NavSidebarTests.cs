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
    [InlineData("Settings")]
    [InlineData("App info")]
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

        sidebar.Find(".pspad-new-area .mud-nav-link").Click();

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

        sidebar.Find(".pspad-new-area .mud-nav-link").Click();

        Assert.Equal(0, newArea);
    }

    [Fact]
    public void NoAreaRowCarriesAMenuAnymore()
    {
        Arrange();

        var sidebar = Render(Areas("Dom", "Praca"));

        Assert.Empty(sidebar.FindComponents<ThingMenu>());
    }

    [Fact]
    public void SettingsAndAppInfoAreBothSidebarRows()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Contains("/settings\"", sidebar.Markup);
        Assert.Contains("/app-info\"", sidebar.Markup);
    }

    [Fact]
    public void ThereIsNoSearchFieldAnymore()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Empty(sidebar.FindAll("input[placeholder='Search']"));
    }

    [Fact]
    public void TheFooterShowsTheDateTimeAndTheLicense()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Contains("pspad-sidebar-footer", sidebar.Markup);
        Assert.Contains("GPL v3", sidebar.Markup);
    }

    [Fact]
    public void TheNewAreaRowCarriesAnAddIcon()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        var newArea = sidebar.Find(".pspad-new-area");
        Assert.Contains("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z", newArea.InnerHtml);
    }

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
