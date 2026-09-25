using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class ListPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ItNamesTheListAndRendersItsOpenTasks()
    {
        var list = NewList("Zakupy");
        Arrange(list, NewTask(list.Id, "Mleko"), NewTask(list.Id, "Chleb"));

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.Contains("Zakupy", page.Markup);
        Assert.Equal(2, page.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void ItShowsAnEmptyStateWhenTheListHasNoTasks()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.Contains("No tasks yet.", page.Markup);
    }

    [Fact]
    public void ItDoesNotShowTheEmptyStateWhenTheListHasTasks()
    {
        var list = NewList("Zakupy");
        Arrange(list, NewTask(list.Id, "Mleko"));

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.DoesNotContain("No tasks yet.", page.Markup);
    }

    [Fact]
    public void TasksFromOtherListsAreNotThere()
    {
        var mine = NewList("Zakupy");
        var other = NewList("Remont");
        Arrange(mine, other, NewTask(mine.Id, "Mleko"), NewTask(other.Id, "Farba"));

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, mine.Id));

        Assert.DoesNotContain("Farba", page.Markup);
    }

    [Fact]
    public void CompletedTasksAreCountedInTheirOwnSection()
    {
        var list = NewList("Zakupy");
        var done = NewTask(list.Id, "Masło");
        done.ApplyAll(TodoTask.Decide(
            done, new CompleteTask(Guid.NewGuid(), User, done.Id), DateTimeOffset.UnixEpoch));
        Arrange(list, NewTask(list.Id, "Mleko"), done);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.Contains("Completed", page.Markup);
        Assert.Contains("1", page.Markup);
    }

    [Fact]
    public void ARecurringTaskIsNeverOverdueHereEither()
    {
        var list = NewList("Regularne");
        var task = NewTask(list.Id, "Read a book");
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Today.AddDays(-7))),
            DateTimeOffset.UnixEpoch));
        Arrange(list, task);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.DoesNotContain("overdue", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItLaysOpenTasksOutInTheGrid()
    {
        var list = NewList("Zakupy");
        Arrange(list, NewTask(list.Id, "Mleko"), NewTask(list.Id, "Chleb"));

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        page.Find(".mud-grid");
    }

    [Fact]
    public void TheListHasExactlyOneFabMenu()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        page.Find(".pspad-fab");
        Assert.Single(page.FindAll(".mud-fab-menu-button"));
    }

    [Fact]
    public void ItDoesNotClaimTheListIsGoneBeforeItHasLoaded()
    {
        var list = NewList("Zakupy");
        ArrangeWithPendingStore(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.Single(page.FindComponents<RowSkeleton>());
        Assert.DoesNotContain("not there anymore", page.Markup);
    }

    [Fact]
    public void ItShowsATitleSkeletonBeforeItHasLoaded()
    {
        var list = NewList("Zakupy");
        ArrangeWithPendingStore(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        var titleSkeleton = page.Find(".mud-skeleton.mb-4");
        var style = titleSkeleton.GetAttribute("style");
        Assert.Contains("width:30%", style);
        Assert.Contains("height:40px", style);
    }

    [Fact]
    public void ANonExistentListShowsItIsGoneInsteadOfCrashing()
    {
        Arrange();

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, Guid.NewGuid()));

        Assert.Contains("not there anymore", page.Markup);
    }

    [Fact]
    public void ADeletedListShowsItIsGone()
    {
        var list = NewList("Zakupy");
        list.ApplyAll(TaskList.Decide(
            list, new DeleteTaskList(Guid.NewGuid(), User, list.Id), DateTimeOffset.UnixEpoch));
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.Contains("not there anymore", page.Markup);
    }

    [Fact]
    public void ADeletedTaskDoesNotReachTheScreen()
    {
        var list = NewList("Zakupy");
        var removed = NewTask(list.Id, "Usunięte");
        removed.ApplyAll(TodoTask.Decide(
            removed, new DeleteTask(Guid.NewGuid(), User, removed.Id), DateTimeOffset.UnixEpoch));
        Arrange(list, NewTask(list.Id, "Mleko"), removed);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.DoesNotContain("Usunięte", page.Markup);
    }

    [Fact]
    public void AddTaskFromTheFabMenuOpensTheNewTaskPanelForThisList()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render(BuildListPageWithDialogs(list.Id));
        var navigation = page.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/lists/{list.Id}");
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[0].Click();

        Assert.EndsWith($"/lists/{list.Id}?task=new&list={list.Id}", navigation.Uri);
        Assert.Empty(page.FindComponent<MudDialogProvider>().FindAll("div.mud-dialog"));
    }

    [Fact]
    public void TheEmptyStateOffersToAddTheFirstTask()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));
        var navigation = page.Services.GetRequiredService<NavigationManager>();
        page.Find(".pspad-empty-state").Click();

        Assert.Single(page.FindComponents<EmptyState>());
        page.Find(".mud-grid-item > .pspad-empty-state");
        Assert.EndsWith($"?task=new&list={list.Id}", navigation.Uri);
    }

    [Fact]
    public void TheBackButtonLeadsToTheListsArea()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        var back = page.Find(".pspad-back-to-area");
        Assert.Equal($"/areas/{list.AreaId}", back.GetAttribute("href"));
        Assert.Equal("Back to area", back.GetAttribute("aria-label"));
        Assert.True(page.Markup.IndexOf("pspad-back-to-area", StringComparison.Ordinal)
                    < page.Markup.IndexOf("Zakupy", StringComparison.Ordinal));
    }

    [Fact]
    public void EachTaskIsItsOwnCard()
    {
        var list = NewList("Zakupy");
        Arrange(list, NewTask(list.Id, "Mleko"), NewTask(list.Id, "Chleb"));

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        var cards = page.FindAll(".mud-grid-item > .pspad-task-card");
        Assert.Equal(2, cards.Count);
    }

    [Fact]
    public async Task ATaskCreatedElsewhereAppearsWithoutReopeningTheList()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));
        var sender = page.Services.GetRequiredService<CommandSender>();
        await page.InvokeAsync(() => sender.SendAsync(
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "Mleko")));

        page.WaitForAssertion(() => Assert.Contains("Mleko", page.Markup));
        Assert.Empty(page.FindComponents<EmptyState>());
    }

    [Fact]
    public async Task TogglingAnOpenTaskCompletesItInTheReplica()
    {
        var list = NewList("Zakupy");
        var task = NewTask(list.Id, "Mleko");
        var replica = Arrange(list, task);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));
        page.Find("input.mud-checkbox-input").Change(true);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.NotNull(stored?.CompletedAt);
    }

    [Fact]
    public async Task RenamingFromTheFabMenuRenamesTheList()
    {
        var list = NewList("Zakupy");
        var replica = Arrange(list);

        var page = Render(BuildListPageWithDialogs(list.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[1].Click();

        var field = page.Find("div.mud-dialog input");
        field.Input("Zakupy tygodniowe");
        page.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.Equal("Zakupy tygodniowe", stored!.Name);
    }

    [Fact]
    public async Task DeletingFromTheFabMenuNavigatesToTheArea()
    {
        var list = NewList("Zakupy");
        var replica = Arrange(list);

        var page = Render(BuildListPageWithDialogs(list.Id));
        var navigation = page.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/lists/{list.Id}");

        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[2].Click();
        page.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.True(stored!.Deleted);
        Assert.EndsWith($"/areas/{list.AreaId}", navigation.Uri);
    }

    [Fact]
    public void TheFabMenuItemsCarryAnAccessibleNameMatchingTheirTooltip()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render(BuildListPageWithDialogs(list.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();

        var items = page.FindAll(".mud-fab-menu-item");
        Assert.Equal("Add task", items[0].GetAttribute("aria-label"));
        Assert.Equal("Rename list", items[1].GetAttribute("aria-label"));
        Assert.Equal("Delete list", items[2].GetAttribute("aria-label"));
    }

    [Fact]
    public void TheInlineAddTaskFieldAndHeaderMenuAreGone()
    {
        var list = NewList("Zakupy");
        Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));

        Assert.Empty(page.FindAll("input[placeholder='Add task']"));
        Assert.Empty(page.FindAll(".pspad-list-menu"));
    }

    // ThingMenu's MudMenu and IDialogService's MudDialogProvider both portal their open
    // content through MudPopoverProvider, so all three must share one render tree.
    RenderFragment BuildListPageWithDialogs(Guid listId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<ListPage>(2);
        builder.AddAttribute(3, nameof(ListPage.ListId), listId);
        builder.CloseComponent();
    };

    InMemoryReplica Arrange(params Aggregate[] documents) => AppTestHost.Arrange(this, User, Today, documents);

    void ArrangeWithPendingStore(params Aggregate[] documents)
    {
        Arrange(documents);
        Services.AddSingleton<IDocumentStore<TaskList>>(new NeverLoadingListStore());
    }

    sealed class NeverLoadingListStore : IDocumentStore<TaskList>
    {
        public Task<TaskList?> LoadAsync(Guid id, CancellationToken ct) =>
            new TaskCompletionSource<TaskList?>().Task;

        public Task<IReadOnlyList<TaskList>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<TaskList>>().Task;
    }

    static TaskList NewList(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, 0),
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
