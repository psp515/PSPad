using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Goals;
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
    public void ATaskDueTomorrowHasItsOwnSection()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Later", Today.AddDays(1)), Due(list.Id, "Now", Today));

        var page = Render<Today>();

        var tomorrow = page.Find(".pspad-day-tomorrow");
        Assert.Contains("Tomorrow", tomorrow.TextContent);
        Assert.Contains("Later", tomorrow.TextContent);
        Assert.DoesNotContain("Now", tomorrow.TextContent);
    }

    [Fact]
    public void TheTomorrowSectionIsAbsentWhenNothingIsDueTomorrow()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Now", Today));

        var page = Render<Today>();

        Assert.Empty(page.FindAll(".pspad-day-tomorrow"));
    }

    [Fact]
    public void UpcomingListsTheRestOfTheWeekGroupedByDay()
    {
        var list = NewList("Zakupy");
        Arrange(
            list,
            Due(list.Id, "Dentist", Today.AddDays(3)),
            Due(list.Id, "Tomorrowish", Today.AddDays(1)),
            Due(list.Id, "Next month", Today.AddDays(8)));

        var page = Render<Today>();

        var upcoming = page.Find(".pspad-day-upcoming");
        Assert.Contains("Upcoming (1)", upcoming.TextContent);
        Assert.Contains("Tue, 15 Sep", upcoming.TextContent);
        Assert.Contains("Dentist", upcoming.TextContent);
        Assert.DoesNotContain("Tomorrowish", upcoming.TextContent);
        Assert.DoesNotContain("Next month", page.Markup);
    }

    [Fact]
    public void ADailyTaskShowsTodayAndAgainTomorrow()
    {
        var list = NewList("Regularne");
        Arrange(list, Recurring(list.Id, "Read a book", Today.AddDays(-7)));

        var page = Render<Today>();

        Assert.Contains("Read a book", page.Find(".pspad-day-tomorrow").TextContent);
        Assert.Empty(page.FindAll(".pspad-day-upcoming"));
    }

    [Fact]
    public async Task TickingTomorrowsOccurrenceCompletesTomorrowNotToday()
    {
        var list = NewList("Regularne");
        var task = Recurring(list.Id, "Read a book", Today.AddDays(-7));
        var replica = Arrange(list, task);

        var page = Render<Today>();
        page.Find(".pspad-day-tomorrow input.mud-checkbox-input").Change(true);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Contains(Today.AddDays(1), stored!.CompletedDays);
        Assert.DoesNotContain(Today, stored.CompletedDays);
    }

    [Fact]
    public void GoalsInProgressSitBetweenTomorrowAndCompleted()
    {
        var list = NewList("Zakupy");
        var done = Due(list.Id, "Masło", Today);
        done.ApplyAll(TodoTask.Decide(
            done, new CompleteTask(Guid.NewGuid(), User, done.Id),
            new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)));
        Arrange(
            list, done, Due(list.Id, "Later", Today.AddDays(1)),
            NewGoal("Run a marathon", GoalStatus.InProgress, Today.AddDays(5)));

        var page = Render<Today>();

        var markup = page.Markup;
        var goals = markup.IndexOf("Goals in progress");
        Assert.True(goals > markup.IndexOf("Later"));
        Assert.True(goals < markup.IndexOf("Completed (1)"));
        Assert.Contains("Run a marathon", page.Find(".pspad-day-goals").TextContent);
    }

    [Fact]
    public void OnlyGoalsInProgressAreListedSoonestFirst()
    {
        Arrange(
            NewGoal("Undated", GoalStatus.InProgress),
            NewGoal("Later", GoalStatus.InProgress, Today.AddDays(20)),
            NewGoal("Sooner", GoalStatus.InProgress, Today.AddDays(2)),
            NewGoal("Done", GoalStatus.Achieved),
            NewGoal("Dropped", GoalStatus.NotAchieved));

        var page = Render<Today>();

        Assert.Equal(
            ["Sooner", "Later", "Undated"],
            page.FindAll(".pspad-day-goals .pspad-goal-summary-name").Select(name => name.TextContent));
    }

    [Fact]
    public void TheGoalsSectionIsAbsentWhenNoGoalIsInProgress()
    {
        Arrange(NewGoal("Done", GoalStatus.Achieved));

        var page = Render<Today>();

        Assert.DoesNotContain("Goals in progress", page.Markup);
    }

    [Fact]
    public void AGoalCountsItsLinkedTasks()
    {
        var list = NewList("Zakupy");
        var goal = NewGoal("Run a marathon", GoalStatus.InProgress);
        var linked = Due(list.Id, "Train", Today);
        linked.ApplyAll(TodoTask.Decide(
            linked, new LinkTaskToGoal(Guid.NewGuid(), User, linked.Id, goal.Id), DateTimeOffset.UnixEpoch));
        Arrange(list, goal, linked);

        var page = Render<Today>();

        Assert.Contains("0 of 1 task done", page.Find(".pspad-day-goals").TextContent);
    }

    [Fact]
    public void ClickingAGoalOpensItsPanel()
    {
        var goal = NewGoal("Run a marathon", GoalStatus.InProgress);
        Arrange(goal);

        var page = Render<Today>();
        page.Find(".pspad-goal-summary").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"?goal={goal.Id}", navigation.Uri);
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
    public void EachTaskIsItsOwnCard()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Mleko", Today), Due(list.Id, "Chleb", Today), Due(list.Id, "Jutro", Today.AddDays(1)));

        var page = Render<Today>();

        Assert.Equal(3, page.FindAll(".mud-grid-item > .pspad-day-task .pspad-task-name").Count);
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

        page.Find(".mud-grid");
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

    static Goal NewGoal(string name, GoalStatus status, DateOnly? dueOn = null)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, status), DateTimeOffset.UnixEpoch));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), User, goal.Id, dueOn), DateTimeOffset.UnixEpoch));
        return goal;
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
