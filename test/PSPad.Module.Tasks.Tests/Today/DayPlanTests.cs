using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class DayPlanTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void EachTaskLandsInTheSectionOfItsDueDate()
    {
        var overdue = Due(Today.AddDays(-1));
        var today = Due(Today);
        var tomorrow = Due(Today.AddDays(1));
        var upcoming = Due(Today.AddDays(2));

        var plan = TodayRule.Plan([upcoming, tomorrow, today, overdue], Today, Utc);

        Assert.Equal(overdue.Id, Assert.Single(plan.Overdue).TaskId);
        Assert.Equal(today.Id, Assert.Single(plan.Today).TaskId);
        Assert.Equal(tomorrow.Id, Assert.Single(plan.Tomorrow).TaskId);
        Assert.Equal(upcoming.Id, Assert.Single(plan.Upcoming).TaskId);
    }

    [Fact]
    public void UpcomingReachesOneWeekAheadAndNoFurther()
    {
        var lastDay = Due(Today.AddDays(7));
        var beyond = Due(Today.AddDays(8));

        var plan = TodayRule.Plan([lastDay, beyond], Today, Utc);

        Assert.Equal(lastDay.Id, Assert.Single(plan.Upcoming).TaskId);
    }

    [Fact]
    public void UpcomingIsOrderedByDate()
    {
        var later = Due(Today.AddDays(5));
        var sooner = Due(Today.AddDays(3));

        var plan = TodayRule.Plan([later, sooner], Today, Utc);

        Assert.Equal([sooner.Id, later.Id], plan.Upcoming.Select(entry => entry.TaskId));
    }

    [Fact]
    public void AStepDueTomorrowPutsItsTaskInTomorrow()
    {
        var task = TodoTaskTests.Existing();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "call"), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, Today.AddDays(1)), Now));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Equal(task.Id, Assert.Single(plan.Tomorrow).TaskId);
    }

    [Fact]
    public void AStarredTaskNotDueYetIsStarredNotTomorrowOrUpcoming()
    {
        var undated = Starred(TodoTaskTests.Existing());
        var tomorrow = Starred(Due(Today.AddDays(1)));
        var nextMonth = Starred(Due(Today.AddDays(30)));

        var plan = TodayRule.Plan([nextMonth, tomorrow, undated], Today, Utc);

        Assert.Equal([undated.Id, tomorrow.Id, nextMonth.Id], plan.Starred.Select(entry => entry.TaskId));
        Assert.Empty(plan.Today);
        Assert.Empty(plan.Tomorrow);
        Assert.Empty(plan.Upcoming);
    }

    [Fact]
    public void AStarredTaskDueTodayOrEarlierStaysInItsDateSection()
    {
        var today = Starred(Due(Today));
        var overdue = Starred(Due(Today.AddDays(-1)));

        var plan = TodayRule.Plan([today, overdue], Today, Utc);

        Assert.Empty(plan.Starred);
        Assert.Single(plan.Today);
        Assert.Single(plan.Overdue);
    }

    [Fact]
    public void CompletedOrRecurringStarredTasksAreNotStarred()
    {
        var done = Starred(TodoTaskTests.Existing());
        done.ApplyAll(TodoTask.Decide(done, new CompleteTask(Guid.NewGuid(), User, done.Id), Now));
        var recurring = Starred(Recurring(RecurrenceRule.Weekly(Today.AddDays(-7), DayOfWeek.Monday)));

        var plan = TodayRule.Plan([done, recurring], Today, Utc);

        Assert.Empty(plan.Starred);
    }

    [Fact]
    public void AStarNeverPutsATaskOnTheTodayRule()
    {
        Assert.Empty(TodayRule.Select([Starred(TodoTaskTests.Existing())], Today));
    }

    [Fact]
    public void AnUndatedTaskIsInNoSection()
    {
        var plan = TodayRule.Plan([TodoTaskTests.Existing()], Today, Utc);

        Assert.Empty(plan.Overdue);
        Assert.Empty(plan.Today);
        Assert.Empty(plan.Tomorrow);
        Assert.Empty(plan.Upcoming);
    }

    [Fact]
    public void ADailyTaskShowsTodayAndOnceMoreTomorrow()
    {
        var task = Recurring(RecurrenceRule.Daily(Today.AddDays(-5)));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Single(plan.Today);
        var next = Assert.Single(plan.Tomorrow);
        Assert.Equal(Today.AddDays(1), next.DueOn);
        Assert.Empty(plan.Upcoming);
        Assert.Empty(plan.Overdue);
    }

    [Fact]
    public void AWeeklyTaskShowsItsNextOccurrenceInUpcoming()
    {
        var task = Recurring(RecurrenceRule.Weekly(Today.AddDays(-7), DayOfWeek.Wednesday));

        var plan = TodayRule.Plan([task], Today, Utc);

        var entry = Assert.Single(plan.Upcoming);
        Assert.Equal(new DateOnly(2026, 9, 16), entry.DueOn);
        Assert.True(entry.Recurring);
        Assert.Empty(plan.Tomorrow);
    }

    [Fact]
    public void ARecurringTaskStillShowsItsNextOccurrenceOnceTodaysIsDone()
    {
        var task = Recurring(RecurrenceRule.Daily(Today.AddDays(-5)));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Today);
        Assert.Single(plan.Tomorrow);
    }

    [Fact]
    public void ACompletedTaskIsNotUpcoming()
    {
        var task = Due(Today.AddDays(1));
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Tomorrow);
    }

    [Fact]
    public void TasksCompletedTodayAreCompleted()
    {
        var task = Due(Today);
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));
        var yesterday = Due(Today);
        yesterday.ApplyAll(TodoTask.Decide(
            yesterday, new CompleteTask(Guid.NewGuid(), User, yesterday.Id), Now.AddDays(-1)));

        var plan = TodayRule.Plan([task, yesterday], Today, Utc);

        Assert.Equal(task.Id, Assert.Single(plan.Completed).TaskId);
    }

    [Fact]
    public void CompletedTodayIsReadInTheUsersZone()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        var task = Due(Today);
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteTask(Guid.NewGuid(), User, task.Id),
            new DateTimeOffset(2026, 9, 11, 20, 0, 0, TimeSpan.Zero)));

        var plan = TodayRule.Plan([task], Today, zone);

        Assert.Single(plan.Completed);
    }

    [Fact]
    public void TodaysTickedOccurrenceIsCompleted()
    {
        var task = Recurring(RecurrenceRule.Daily(Today.AddDays(-5)));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Equal(task.Id, Assert.Single(plan.Completed).TaskId);
    }

    [Fact]
    public void DeletedTasksAreInNoSection()
    {
        var task = Due(Today.AddDays(1));
        task.ApplyAll(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), User, task.Id), Now));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Tomorrow);
    }

    [Fact]
    public void OverdueAndTodayMatchTheTodayRule()
    {
        var tasks = new[] { Due(Today.AddDays(-2)), Due(Today), Due(Today.AddDays(1)) };

        var plan = TodayRule.Plan(tasks, Today, Utc);

        Assert.Equal(
            TodayRule.Select(tasks, Today).Select(entry => entry.TaskId),
            plan.Overdue.Concat(plan.Today).Select(entry => entry.TaskId));
    }

    static TodoTask Due(DateOnly day)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), Now));
        return task;
    }

    static TodoTask Starred(TodoTask task)
    {
        task.ApplyAll(TodoTask.Decide(task, new StarTask(Guid.NewGuid(), User, task.Id, true), Now));
        return task;
    }

    static TodoTask Recurring(RecurrenceRule rule)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule), Now));
        return task;
    }
}
