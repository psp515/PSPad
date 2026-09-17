using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
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

        page.Find(".pspad-grid");
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
    public void NewListIsOffered()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Contains("New list", page.Markup);
    }

    [Fact]
    public async Task ClickingACardsAddTaskIconOpensADialogThatCreatesTheTaskInTheReplica()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy", 0);
        var replica = Arrange(area, shopping);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-add-task").Click();
        var dialog = page.FindComponent<MudDialogProvider>();
        dialog.Find("input[placeholder='Task name']").Input("Kup farbę");
        dialog.FindAll("button").Last().Click();

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        Assert.Contains(tasks, task => task.ListId == shopping.Id && task.Name == "Kup farbę");
    }

    [Fact]
    public async Task TheAddTaskDialogAlsoSetsDueDatePriorityGoalAndStar()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy", 0);
        var goal = NewGoal("Remont domu");
        var replica = Arrange(area, shopping, goal);

        var page = Render(BuildAreaBoardWithDialogs(area.Id));
        page.Find(".pspad-add-task").Click();
        var dialog = page.FindComponent<MudDialogProvider>();
        dialog.Find("input[placeholder='Task name']").Input("Kup farbę");
        dialog.Find(".pspad-add-task-priority").MouseDown();
        page.FindAll(".mud-list-item")[(int)Priority.High].Click();
        dialog.Find(".pspad-add-task-goal").MouseDown();
        page.FindAll(".mud-list-item").Last().Click();
        dialog.Find(".pspad-add-task-star").Click();
        dialog.FindAll("button").Last().Click();

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        var created = Assert.Single(tasks, task => task.ListId == shopping.Id && task.Name == "Kup farbę");
        Assert.Equal(Priority.High, created.Priority);
        Assert.Equal(goal.Id, created.GoalId);
        Assert.True(created.Starred);
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

    static Goal NewGoal(string name)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(
            null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        return goal;
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
