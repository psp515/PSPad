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
    public void ATaskFromASharedListDueTodayShowsUnderToday()
    {
        var owner = Guid.NewGuid();
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), owner, Guid.NewGuid(), Guid.NewGuid(), "Wspólna"),
            DateTimeOffset.UnixEpoch));
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), owner, Guid.NewGuid(), list.Id, "Oddać książki"),
            DateTimeOffset.UnixEpoch));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), owner, task.Id, Today), DateTimeOffset.UnixEpoch));
        Arrange(list, task);

        var page = Render<Today>();

        Assert.Contains("Oddać książki", page.Find(".pspad-day-anytime").TextContent);
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
    public void ATaskDueTomorrowIsComingUp()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Later", Today.AddDays(1)), Due(list.Id, "Now", Today));

        var page = Render<Today>();

        var upcoming = page.Find(".pspad-day-comingup");
        Assert.Contains("Later", upcoming.TextContent);
        Assert.DoesNotContain("Now", upcoming.TextContent);
    }

    [Fact]
    public void StarredTasksNotDueYetGetTheirOwnSectionAfterToday()
    {
        var list = NewList("Zakupy");
        var undated = New(list.Id, "Important");
        undated.ApplyAll(TodoTask.Decide(
            undated, new StarTask(Guid.NewGuid(), User, undated.Id, true), DateTimeOffset.UnixEpoch));
        var later = Due(list.Id, "Important later", Today.AddDays(1));
        later.ApplyAll(TodoTask.Decide(
            later, new StarTask(Guid.NewGuid(), User, later.Id, true), DateTimeOffset.UnixEpoch));
        Arrange(list, undated, later, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        var starred = page.Find(".pspad-day-starred").TextContent;
        Assert.Contains("Starred", starred);
        Assert.Contains("Important", starred);
        Assert.Contains("Important later", starred);
        Assert.DoesNotContain("Important", page.Find(".pspad-day-anytime").TextContent);
        Assert.Empty(page.FindAll(".pspad-day-comingup"));
        Assert.True(page.Markup.IndexOf("pspad-day-anytime") < page.Markup.IndexOf("pspad-day-starred"));
    }

    [Fact]
    public void TheStarredSectionIsAbsentWhenNothingIsStarred()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.Empty(page.FindAll(".pspad-day-starred"));
    }

    [Fact]
    public void TheUpcomingSectionIsAbsentWhenNothingIsComingUp()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Now", Today));

        var page = Render<Today>();

        Assert.Empty(page.FindAll(".pspad-day-comingup"));
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

        var upcoming = page.Find(".pspad-day-comingup");
                Assert.Contains("Tue, 15 Sep", upcoming.TextContent);
        Assert.Contains("Dentist", upcoming.TextContent);
        Assert.Contains("Tomorrowish", upcoming.TextContent);
        Assert.DoesNotContain("Next month", page.Markup);
    }

    [Fact]
    public void ADailyTaskShowsTodayAndAgainTomorrow()
    {
        var list = NewList("Regularne");
        Arrange(list, Recurring(list.Id, "Read a book", Today.AddDays(-7)));

        var page = Render<Today>();

        Assert.Contains("Read a book", page.Find(".pspad-day-anytime").TextContent);
        Assert.Contains("Read a book", page.Find(".pspad-day-comingup").TextContent);
    }

    [Fact]
    public async Task TickingTomorrowsOccurrenceCompletesTomorrowNotToday()
    {
        var list = NewList("Regularne");
        var task = Recurring(list.Id, "Read a book", Today.AddDays(-7));
        var replica = Arrange(list, task);

        var page = Render<Today>();
        page.Find(".pspad-day-comingup input.mud-checkbox-input").Change(true);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Contains(Today.AddDays(1), stored!.CompletedDays);
        Assert.DoesNotContain(Today, stored.CompletedDays);
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
        Assert.Equal("Today", page.Find(".pspad-day-label").TextContent);
        Assert.Empty(page.FindAll(".pspad-day-today-button"));
        Assert.DoesNotContain("Nothing planned for today.", page.Markup);
    }

    [Fact]
    public void ItDoesNotClaimNothingIsDueWhenSomethingIsOverdue()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Buy milk", Today.AddDays(-1)));

        var page = Render<Today>();

        Assert.DoesNotContain("Nothing planned for today.", page.Markup);
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

    [Fact]
    public void GoalsAreNotOnMyDay()
    {
        var list = NewList("Zakupy");
        Arrange(list, NewGoal("Run a marathon", GoalStatus.InProgress), Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.DoesNotContain("Run a marathon", page.Markup);
    }

    [Fact]
    public void TimedTasksAreScheduledBeforeAnyTime()
    {
        var list = NewList("Praca");
        Arrange(list, Timed(Due(list.Id, "Sprint planning", Today), 9, 30, 11, 0), Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        var schedule = page.Find(".pspad-day-schedule").TextContent;
        Assert.Equal("09:30", page.Find(".pspad-schedule-start").TextContent);
        Assert.Equal("11:00", page.Find(".pspad-schedule-end").TextContent);
        Assert.Contains("Sprint planning", schedule);
        Assert.Contains("Today, any time", page.Find(".pspad-day-anytime").TextContent);
        Assert.Contains("Mleko", page.Find(".pspad-day-anytime").TextContent);
        Assert.True(page.Markup.IndexOf("pspad-day-schedule") < page.Markup.IndexOf("pspad-day-anytime"));
    }

    [Fact]
    public void ATaskWithoutAnEndShowsOnlyItsStart()
    {
        var list = NewList("Praca");
        var task = Due(list.Id, "Standup", Today);
        task.ApplyAll(TodoTask.Decide(task, new SetTaskTime(Guid.NewGuid(), User, task.Id,
            TaskTime.Of(new TimeOnly(9, 30), null)), DateTimeOffset.UnixEpoch));
        Arrange(list, task);

        var page = Render<Today>();

        Assert.Equal("09:30", page.Find(".pspad-schedule-start").TextContent);
        Assert.Empty(page.FindAll(".pspad-schedule-end"));
    }

    [Fact]
    public void TheDayComesFromTheQuery()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Later", Today.AddDays(1)), Due(list.Id, "Now", Today));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/?day={Today.AddDays(1):yyyy-MM-dd}");

        var page = Render<Today>();

        Assert.Contains("Tomorrow, any time", page.Find(".pspad-day-anytime").TextContent);
        Assert.Contains("Later", page.Markup);
        Assert.DoesNotContain("Now", page.Markup);
        Assert.Empty(page.FindAll(".pspad-day-comingup"));
    }

    [Fact]
    public void TheNextArrowMovesToTomorrow()
    {
        Arrange(NewList("Zakupy"));
        var page = Render<Today>();

        page.Find(".pspad-day-next").Click();

        Assert.EndsWith($"?day={Today.AddDays(1):yyyy-MM-dd}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void APastDayShowsOnlyCompletedOpen()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Missed", Today.AddDays(-1)));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/?day={Today.AddDays(-1):yyyy-MM-dd}");

        var page = Render<Today>();

        Assert.Contains("Nothing completed on", page.Find(".pspad-day-completed").TextContent);
        Assert.DoesNotContain("Missed", page.Markup);
    }

    [Fact]
    public void ClickingATaskKeepsTheDay()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Later", Today.AddDays(1));
        Arrange(list, task);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/?day={Today.AddDays(1):yyyy-MM-dd}");

        var page = Render<Today>();
        page.Find(".pspad-task-name").Click();

        Assert.Contains($"day={Today.AddDays(1):yyyy-MM-dd}&task={task.Id}", navigation.Uri);
    }

    [Fact]
    public void NothingPlannedSaysSoForTheDay()
    {
        Arrange(NewList("Zakupy"));

        var page = Render<Today>();

        Assert.Contains("Nothing planned for today.", page.Find(".pspad-day-anytime").TextContent);
    }

    static TodoTask Timed(TodoTask task, int sh, int sm, int eh, int em)
    {
        task.ApplyAll(TodoTask.Decide(task, new SetTaskTime(Guid.NewGuid(), User, task.Id,
            TaskTime.Of(new TimeOnly(sh, sm), new TimeOnly(eh, em))), DateTimeOffset.UnixEpoch));
        return task;
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
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
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
