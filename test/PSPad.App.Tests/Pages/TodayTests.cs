using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class TodayTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void OverdueTasksGetTheirOwnSection()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Buy milk", Today.AddDays(-1)));

        var page = Render<Today>();

        Assert.Contains("Overdue", page.Markup);
        Assert.Contains("Buy milk", page.Markup);
    }

    [Fact]
    public void TheOverdueSectionIsAbsentWhenNothingIsOverdue()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Buy milk", Today));

        var page = Render<Today>();

        Assert.DoesNotContain("Overdue", page.Markup);
        Assert.Contains("Buy milk", page.Markup);
    }

    [Fact]
    public void ARecurringTaskMissedYesterdayIsNotOverdue()
    {
        var list = NewList("Regularne");
        Arrange(list, Recurring(list.Id, "Read a book", Today.AddDays(-7)));

        var page = Render<Today>();

        Assert.Contains("Read a book", page.Markup);
        Assert.DoesNotContain("Overdue", page.Markup);
        Assert.DoesNotContain("overdue", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATaskDueTomorrowIsNotOnTheScreen()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Later", Today.AddDays(1)));

        var page = Render<Today>();

        Assert.DoesNotContain("Later", page.Markup);
    }

    [Fact]
    public void ATaskCompletedTodayMovesToTheCompletedSection()
    {
        var list = NewList("Zakupy");
        var done = Due(list.Id, "Masło", Today);
        done.ApplyAll(TodoTask.Decide(
            done, new CompleteTask(Guid.NewGuid(), User, done.Id),
            new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)));
        Arrange(list, done, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.Contains("Completed (1)", page.Markup);
        Assert.Contains("Masło", page.Markup);

        var beforeCompletedSection = page.Markup[..page.Markup.IndexOf("Completed (1)")];
        Assert.DoesNotContain("Masło", beforeCompletedSection);
        Assert.Contains("Mleko", beforeCompletedSection);
    }

    [Fact]
    public void ThereIsNoEmptyOverdueBoxWhenEverythingIsDueToday()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.DoesNotContain("Overdue", page.Markup);
        Assert.Single(page.FindAll(".mud-paper"));
    }

    [Fact]
    public void EachRowCarriesItsListName()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.Contains("Zakupy", page.Markup);
    }

    [Fact]
    public async Task TogglingATaskCompletesItInTheReplica()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Mleko", Today);
        var replica = Arrange(list, task);

        var page = Render<Today>();
        page.Find("input.mud-checkbox-input").Change(true);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.NotNull(stored?.CompletedAt);
    }

    [Fact]
    public void ClickingATaskAppendsItToTheQueryString()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Mleko", Today);
        Arrange(list, task);

        var page = Render<Today>();
        page.Find(".pspad-task-name").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"?task={task.Id}", navigation.Uri);
    }

    [Fact]
    public void ItLaysTodaysTasksOutInTheGrid()
    {
        var list = NewList("Zakupy");
        Arrange(
            list,
            Due(list.Id, "Mleko", Today),
            Due(list.Id, "Chleb", Today),
            Due(list.Id, "Masło", Today),
            Due(list.Id, "Jajka", Today));

        var page = Render<Today>();

        page.Find(".pspad-grid");
    }

    [Fact]
    public void ItDoesNotClaimNothingIsDueBeforeItHasLoaded()
    {
        ArrangeWithPendingStore();

        var page = Render<Today>();

        Assert.Single(page.FindComponents<RowSkeleton>());
        Assert.DoesNotContain("Nothing due today", page.Markup);
    }

    [Fact]
    public void ItDoesNotClaimNothingIsDueWhenSomethingIsOverdue()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Buy milk", Today.AddDays(-1)));

        var page = Render<Today>();

        Assert.DoesNotContain("Nothing due today", page.Markup);
    }

    [Fact]
    public async Task CompletingATaskDropsTheTodayCount()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Mleko", Today);
        Arrange(list, task);

        var counts = new SidebarCounts(
            Services.GetRequiredService<IDocumentStore<TodoTask>>(),
            Services.GetRequiredService<IDocumentStore<Inbox>>(),
            Services.GetRequiredService<AppState>());
        await counts.RefreshAsync();
        Assert.Equal(1, counts.Today);

        var page = Render<Today>();
        page.Find("input.mud-checkbox-input").Change(true);
        await counts.RefreshAsync();

        Assert.Equal(0, counts.Today);
    }

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, Today, documents);

    void ArrangeWithPendingStore()
    {
        Arrange();
        Services.AddSingleton<IDocumentStore<TodoTask>>(new NeverLoadingTaskStore());
    }

    sealed class NeverLoadingTaskStore : IDocumentStore<TodoTask>
    {
        public Task<TodoTask?> LoadAsync(Guid id, CancellationToken ct) =>
            new TaskCompletionSource<TodoTask?>().Task;

        public Task<IReadOnlyList<TodoTask>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<TodoTask>>().Task;
    }

    static TaskList NewList(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask Due(Guid listId, string name, DateOnly due)
    {
        var task = New(listId, name);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Recurring(Guid listId, string name, DateOnly from)
    {
        var task = New(listId, name);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(from)),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask New(Guid listId, string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, name),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
