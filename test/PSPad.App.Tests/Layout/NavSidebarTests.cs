using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
    public void GoalsAndHistoryAreNotSidebarRows()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.DoesNotContain("/goals\"", sidebar.Markup);
        Assert.DoesNotContain("/history\"", sidebar.Markup);
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
            .Add(p => p.Email, "kolberu@gmail.com")
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
            .Add(p => p.Email, "kolberu@gmail.com")
            .Add(p => p.UserId, User)
            .Add(p => p.OnNewArea, EventCallback.Factory.Create(this, () => newArea++)));

        sidebar.Find("button").Click();

        Assert.Equal(1, newArea);
    }

    IRenderedComponent<NavSidebar> Render(IReadOnlyList<Area> areas) =>
        Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, areas)
            .Add(p => p.Email, "kolberu@gmail.com")
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
