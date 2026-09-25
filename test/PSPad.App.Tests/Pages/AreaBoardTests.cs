using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AreaBoardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItNamesTheArea()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Contains("Dom", page.Markup);
    }

    [Fact]
    public void ItListsOnlyTheListsOfThatArea()
    {
        var mine = NewArea("Dom");
        var other = NewArea("Praca");
        Arrange(mine, other, NewList(mine.Id, "Zakupy", 0), NewList(other.Id, "Sprint", 0));

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, mine.Id));

        Assert.Contains("Zakupy", page.Markup);
        Assert.DoesNotContain("Sprint", page.Markup);
    }

    [Fact]
    public void ItOrdersListsByPositionNotCreationOrder()
    {
        var area = NewArea("Dom");
        var remont = NewList(area.Id, "Remont", 2);
        var zakupy = NewList(area.Id, "Zakupy", 0);
        var ogrod = NewList(area.Id, "Ogród", 1);
        Arrange(area, remont, zakupy, ogrod);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        var markup = page.Markup;
        Assert.True(markup.IndexOf("Zakupy", StringComparison.Ordinal)
            < markup.IndexOf("Ogród", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("Ogród", StringComparison.Ordinal)
            < markup.IndexOf("Remont", StringComparison.Ordinal));
    }

    [Fact]
    public void ADeletedAreasScreenDoesNotRenderItsName()
    {
        var area = NewArea("Dom");
        area.ApplyAll(Area.Decide(
            area, new DeleteArea(Guid.NewGuid(), User, area.Id), DateTimeOffset.UnixEpoch));
        Arrange(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.DoesNotContain("Dom", page.Markup);
    }

    [Fact]
    public void ItHidesDeletedLists()
    {
        var area = NewArea("Dom");
        var kept = NewList(area.Id, "Zakupy", 0);
        var removed = NewList(area.Id, "Remont", 1);
        removed.ApplyAll(TaskList.Decide(
            removed, new DeleteTaskList(Guid.NewGuid(), User, removed.Id), DateTimeOffset.UnixEpoch));
        Arrange(area, kept, removed);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Contains("Zakupy", page.Markup);
        Assert.DoesNotContain("Remont", page.Markup);
    }

    [Fact]
    public void EachListIsACard()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy", 0);
        var repairs = NewList(area.Id, "Remont", 1);
        Arrange(area, shopping, repairs);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Equal(2, page.FindComponents<PSPad.App.Components.ListCard>().Count);
    }

    [Fact]
    public void ACardShowsOnlyItsOwnTasks()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy", 0);
        var repairs = NewList(area.Id, "Remont", 1);
        Arrange(area, shopping, repairs, NewTask(shopping.Id, "Mleko"), NewTask(repairs.Id, "Farba"));

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));
        var first = page.FindComponents<PSPad.App.Components.ListCard>()[0];

        Assert.Contains("Mleko", first.Markup);
        Assert.DoesNotContain("Farba", first.Markup);
    }

    [Fact]
    public void ItLaysListCardsOutInTheGrid()
    {
        var area = NewArea("Dom");
        Arrange(area, NewList(area.Id, "Zakupy", 0));

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        page.Find(".mud-grid");
    }

    [Fact]
    public void ItShowsCardSkeletonsBeforeItHasLoaded()
    {
        var area = NewArea("Dom");
        ArrangeWithPendingStore(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Single(page.FindComponents<CardSkeleton>());
        Assert.DoesNotContain("This area is not there anymore", page.Markup);
    }

    [Fact]
    public void ClickingACardsAddTaskIconOpensTheNewTaskPanelForThatList()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy", 0);
        Arrange(area, shopping);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/areas/{area.Id}");

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));
        page.Find(".pspad-add-task").Click();

        Assert.EndsWith($"/areas/{area.Id}?task=new&list={shopping.Id}", navigation.Uri);
    }

    [Fact]
    public void AnAreaWithNoListsShowsTheEmptyStateThatCreatesAList()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        var empty = page.FindComponent<EmptyState>();
        Assert.Contains("No lists yet.", empty.Markup);
        empty.Find(".pspad-empty-state").Click();

        page.Find("div.mud-dialog input");
    }

    [Fact]
    public void AnAreaWithListsHasNoEmptyState()
    {
        var area = NewArea("Dom");
        Arrange(area, NewList(area.Id, "Zakupy", 0));

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Empty(page.FindComponents<EmptyState>());
    }

    [Fact]
    public async Task ATaskSentFromElsewhereShowsUpWithoutAReload()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy", 0);
        Arrange(area, shopping);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));
        var sender = Services.GetRequiredService<PSPad.App.State.Dispatch.CommandSender>();
        await page.InvokeAsync(() => sender.SendAsync(
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), shopping.Id, "Kup farbę")));

        page.WaitForAssertion(() => Assert.Contains("Kup farbę", page.Markup));
    }

    [Fact]
    public async Task DeletingAListFromItsCardRemovesItFromTheScreen()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy", 0);
        var replica = Arrange(area, list);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-list-menu button").Click();
        page.FindAll(".mud-menu-item").Last().Click();

        var dialog = page.FindComponent<MudDialogProvider>();
        dialog.FindAll("button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.True(stored!.Deleted);
    }

    [Fact]
    public void TheAreaHasExactlyOneFabMenu()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        page.Find(".pspad-fab");
        Assert.Single(page.FindAll(".mud-fab-menu-button"));
    }

    [Fact]
    public void TheFabMenuOffersThreeIconOnlyItems()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();

        var items = page.FindAll(".mud-fab-menu-item");
        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.NotNull(item.QuerySelector("svg")));
        Assert.All(items, item => Assert.True(string.IsNullOrWhiteSpace(item.TextContent)));
    }

    [Fact]
    public void TheFabMenuItemsCarryAnAccessibleNameMatchingTheirTooltip()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();

        var items = page.FindAll(".mud-fab-menu-item");
        Assert.Equal("New list", items[0].GetAttribute("aria-label"));
        Assert.Equal("Edit area", items[1].GetAttribute("aria-label"));
        Assert.Equal("Delete area", items[2].GetAttribute("aria-label"));
    }

    [Fact]
    public async Task NewListFromTheFabMenuCreatesItInTheReplica()
    {
        var area = NewArea("Dom");
        var replica = Arrange(area);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[0].Click();

        var field = page.Find("div.mud-dialog input");
        field.Input("Ogród");
        page.FindAll("div.mud-dialog button").Last().Click();

        var lists = await replica.LoadAllAsync<TaskList>(User);
        Assert.Contains(lists, list => list.AreaId == area.Id && list.Name == "Ogród");
    }

    [Fact]
    public void EditAreaFromTheFabMenuOpensTheAreaPanel()
    {
        var area = NewArea("Dom");
        Arrange(area);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/areas/{area.Id}");

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[1].Click();

        Assert.EndsWith($"/areas/{area.Id}?area={area.Id}", navigation.Uri);
    }

    [Fact]
    public async Task DeletingTheAreaFromItsFabMenuNavigatesHome()
    {
        var area = NewArea("Dom");
        var replica = Arrange(area);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        var navigation = page.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/areas/{area.Id}");

        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[2].Click();
        page.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<Area>(area.Id);
        Assert.True(stored!.Deleted);
        Assert.Equal(navigation.BaseUri, navigation.Uri);
    }

    // Both ThingMenu's MudMenu and IDialogService's MudDialogProvider portal their open
    // content through MudPopoverProvider, so all three must share one render tree.
    RenderFragment BuildAreaBoardWithDialogs(Guid areaId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<AreaBoard>(2);
        builder.AddAttribute(3, nameof(AreaBoard.AreaId), areaId);
        builder.CloseComponent();
    };

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12), documents);

    void ArrangeWithPendingStore(params Aggregate[] documents)
    {
        Arrange(documents);
        Services.AddSingleton<IDocumentStore<Area>>(new NeverLoadingAreaStore());
    }

    sealed class NeverLoadingAreaStore : IDocumentStore<Area>
    {
        public Task<Area?> LoadAsync(Guid id, CancellationToken ct) =>
            new TaskCompletionSource<Area?>().Task;

        public Task<IReadOnlyList<Area>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<Area>>().Task;
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name, int position)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, position),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask NewTask(Guid listId, string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, name),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
