using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.App.Theme;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.App.Sync;
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
    [InlineData("Statistics")]
    [InlineData("List snapshots")]
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
    public void GoalsAndStatisticsAreBothSidebarRows()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Contains("/goals\"", sidebar.Markup);
        Assert.Contains("/statistics\"", sidebar.Markup);
    }

    [Fact]
    public void ListSnapshotsIsASidebarRowAfterStatistics()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        Assert.Contains("/snapshots\"", sidebar.Markup);
        var markup = sidebar.Markup;
        Assert.True(markup.IndexOf("/statistics\"") < markup.IndexOf("/snapshots\""));
    }

    [Fact]
    public void NewAreaIsThereEvenWithNoAreasAtAll()
    {
        Arrange();

        var sidebar = Render([]);

        Assert.Contains("New area", sidebar.Markup);
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
    public void TheSidebarShowsSharedWithMeOnlyWhenSomethingIsShared()
    {
        Arrange();

        var without = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, Areas("Dom"))
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User)
            .Add(p => p.HasShared, false));
        Assert.DoesNotContain("Shared with me", without.Markup);

        var with = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, Areas("Dom"))
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User)
            .Add(p => p.HasShared, true));
        Assert.Contains("Shared with me", with.Markup);
        Assert.Contains($"{SharedWithMe.Href}\"", with.Markup);
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
        Assert.Contains("PSPad", sidebar.Markup);
        Assert.Contains("GPL v3", sidebar.Markup);
    }

    [Fact]
    public void TheFooterSplitsTheDateAndTheSignatureAcrossTwoLines()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        var footer = sidebar.Find(".pspad-sidebar-footer");
        Assert.Equal(2, footer.Children.Length);
    }

    [Fact]
    public void TheFooterGainsAConnectionStatusOnlyWhenSomethingIsWrong()
    {
        Arrange();
        var reachability = Services.GetRequiredService<ServerReachability>();

        var healthy = Render(Areas("Dom"));

        Assert.Empty(healthy.FindComponents<ConnectionStatus>().Single().FindAll(".pspad-connection-status"));

        reachability.Failed(browserIsOnline: true);
        var troubled = Render(Areas("Dom"));

        Assert.NotEmpty(troubled.FindAll(".pspad-connection-status"));
    }

    [Fact]
    public void TheNewAreaRowCarriesAnAddIcon()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"));

        var newArea = sidebar.Find(".pspad-new-area");
        Assert.Contains("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z", newArea.InnerHtml);
    }

    [Fact]
    public async Task CountsSitApartFromTheLabel()
    {
        var replica = Arrange();
        await replica.SaveAsync(InboxWith("milk", "bread", "eggs"));
        await Services.GetRequiredService<SidebarCounts>().RefreshAsync();

        var nav = Render(Areas("Dom"));

        var inbox = nav.FindAll(".mud-nav-link").First(link => link.TextContent.Contains("Inbox"));
        Assert.Equal("3", inbox.QuerySelector(".pspad-nav-count")!.TextContent.Trim());
        Assert.DoesNotContain("(3)", inbox.TextContent);
        var myDay = nav.FindAll(".mud-nav-link").First(link => link.TextContent.Contains("My Day"));
        Assert.Null(myDay.QuerySelector(".pspad-nav-count"));
    }

    [Fact]
    public void AreasHaveACaption()
    {
        Arrange();

        var nav = Render(Areas("Dom"));

        Assert.Equal("Areas", nav.FindAll(".pspad-nav-caption").Last().TextContent.Trim());
    }

    [Fact]
    public void ItListsActiveBudgetsAndHidesArchivedOnes()
    {
        Arrange();

        var sidebar = Render(Areas("Dom"), BudgetNamed("Personal"), BudgetNamed("Old", archived: true));

        Assert.Contains("Personal", sidebar.Markup);
        Assert.DoesNotContain(">Old<", sidebar.Markup);
        Assert.NotEmpty(sidebar.FindAll("a[href='/budgets']"));
    }

    [Fact]
    public void CollapsingAreasHidesTheirRowsAndShowsTheCount()
    {
        Arrange();
        var sidebar = Render(Areas("Dom", "Praca"));

        sidebar.Find("button[aria-label='Collapse areas']").Click();

        Assert.DoesNotContain("Praca", sidebar.Markup);
        Assert.Contains("Areas · 2", sidebar.Markup);
        Assert.False(Services.GetRequiredService<NavGroupState>().IsExpanded(NavGroupState.Areas));
    }

    static Budget BudgetNamed(string name, bool archived = false)
    {
        var budget = new Budget();
        var id = Guid.NewGuid();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), User, id, name), DateTimeOffset.UnixEpoch));
        if (archived)
        {
            budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), User, id), DateTimeOffset.UnixEpoch));
        }

        return budget;
    }

    IRenderedComponent<NavSidebar> Render(IReadOnlyList<Area> areas, params Budget[] budgets) =>
        Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, areas)
            .Add(p => p.Budgets, budgets)
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.UserId, User));

    InMemoryReplica Arrange()
    {
        var today = new DateOnly(2026, 9, 12);
        var replica = AppTestHost.Arrange(this, User, today);
        Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
            new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
            new AppState { UserId = User, Today = today }));
        return replica;
    }

    static Inbox InboxWith(params string[] texts)
    {
        var inbox = new Inbox();
        var inboxId = Guid.NewGuid();
        inbox.ApplyAll(Inbox.Decide(null, new CreateInbox(Guid.NewGuid(), User, inboxId), DateTimeOffset.UnixEpoch));
        foreach (var text in texts)
        {
            inbox.ApplyAll(Inbox.Decide(inbox,
                new CaptureToInbox(Guid.NewGuid(), User, inboxId, Guid.NewGuid(), text), DateTimeOffset.UnixEpoch));
        }

        return inbox;
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
