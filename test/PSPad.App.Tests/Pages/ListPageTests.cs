using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
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

        page.Find(".pspad-grid");
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
    public async Task TypingIntoTheAddTaskFieldCreatesTheTaskInTheReplica()
    {
        var list = NewList("Zakupy");
        var replica = Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));
        var input = page.Find("input[placeholder='Add task']");
        input.Input("Kup chleb");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        Assert.Contains(tasks, task => task.ListId == list.Id && task.Name == "Kup chleb");
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
    public async Task RenamingFromTheTitleMenuRenamesTheList()
    {
        var list = NewList("Zakupy");
        var replica = Arrange(list);

        var page = Render(BuildListPageWithDialogs(list.Id));
        page.Find(".pspad-list-menu button").Click();
        page.FindAll(".mud-menu-item")[0].Click();

        var field = page.Find("div.mud-dialog input");
        field.Input("Zakupy tygodniowe");
        page.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.Equal("Zakupy tygodniowe", stored!.Name);
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
