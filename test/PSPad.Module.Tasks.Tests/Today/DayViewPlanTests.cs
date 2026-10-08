using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class DayViewPlanTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateOnly Today = new(2026, 10, 8);
    static readonly DateTimeOffset Now = new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void TimedTasksAreScheduledByStartAndUntimedOnesAreAnyTime()
    {
        var late = Timed(Due(Today), 14, 0);
        var early = Timed(Due(Today), 9, 30);
        var loose = Due(Today);

        var plan = TodayRule.Plan([late, loose, early], Today, Today, Utc);

        Assert.Equal([early.Id, late.Id], plan.Scheduled.Select(entry => entry.TaskId));
        Assert.Equal(new TimeOnly(9, 30), plan.Scheduled[0].Time!.Start);
        Assert.Equal(loose.Id, Assert.Single(plan.AnyTime).TaskId);
    }

    [Fact]
    public void ATimedOverdueTaskStaysOverdue()
    {
        var task = Timed(Due(Today.AddDays(-2)), 9, 0);

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Equal(task.Id, Assert.Single(plan.Overdue).TaskId);
        Assert.Empty(plan.Scheduled);
    }

    [Fact]
    public void AFutureDayShowsWhatIsDueOnItAndNothingOverdue()
    {
        var day = Today.AddDays(3);
        var overdue = Due(Today.AddDays(-1));
        var onDay = Due(day);
        var timed = Timed(Due(day), 16, 0);
        var other = Due(Today.AddDays(1));

        var plan = TodayRule.Plan([overdue, onDay, timed, other], day, Today, Utc);

        Assert.Empty(plan.Overdue);
        Assert.Equal(onDay.Id, Assert.Single(plan.AnyTime).TaskId);
        Assert.Equal(timed.Id, Assert.Single(plan.Scheduled).TaskId);
        Assert.Empty(plan.ComingUp);
    }

    [Fact]
    public void AFutureDayIgnoresTheLeadTime()
    {
        var day = Today.AddDays(40);
        var task = Due(day);

        var plan = TodayRule.Plan([task], day, Today, Utc);

        Assert.Equal(task.Id, Assert.Single(plan.AnyTime).TaskId);
    }

    [Fact]
    public void ARecurringTaskShowsOnAFutureDayItOccursOn()
    {
        var task = Recurring(RecurrenceRule.Daily(Today.AddDays(-7)));

        var plan = TodayRule.Plan([task], Today.AddDays(2), Today, Utc);

        var entry = Assert.Single(plan.AnyTime);
        Assert.Equal(Today.AddDays(2), entry.DueOn);
    }

    [Fact]
    public void APastDayShowsOnlyWhatWasCompletedOnIt()
    {
        var yesterday = Today.AddDays(-1);
        var overdue = Due(yesterday);
        var done = Due(yesterday);
        done.ApplyAll(TodoTask.Decide(done, new CompleteTask(Guid.NewGuid(), User, done.Id),
            new DateTimeOffset(2026, 10, 7, 18, 0, 0, TimeSpan.Zero)));
        var starred = Star(TodoTaskTests.Existing());

        var plan = TodayRule.Plan([overdue, done, starred], yesterday, Today, Utc);

        Assert.Equal(done.Id, Assert.Single(plan.Completed).TaskId);
        Assert.Empty(plan.Overdue);
        Assert.Empty(plan.AnyTime);
        Assert.Empty(plan.Scheduled);
        Assert.Empty(plan.Starred);
        Assert.Empty(plan.ComingUp);
    }

    [Fact]
    public void StarredOnAFutureDayLeavesOutTasksDueByThatDay()
    {
        var day = Today.AddDays(3);
        var undated = Star(TodoTaskTests.Existing());
        var dueOnDay = Star(Due(day));
        var later = Star(Due(day.AddDays(1)));

        var plan = TodayRule.Plan([undated, dueOnDay, later], day, Today, Utc);

        Assert.Equal([undated.Id, later.Id], plan.Starred.Select(entry => entry.TaskId));
        Assert.Equal(dueOnDay.Id, Assert.Single(plan.AnyTime).TaskId);
    }

    [Fact]
    public void ComingUpMergesTomorrowAndTheRestOfTheWeekOnTodayOnly()
    {
        var tomorrow = Due(Today.AddDays(1));
        var later = Due(Today.AddDays(4));

        var onToday = TodayRule.Plan([later, tomorrow], Today, Today, Utc);
        var onTomorrow = TodayRule.Plan([later, tomorrow], Today.AddDays(1), Today, Utc);

        Assert.Equal([tomorrow.Id, later.Id], onToday.ComingUp.Select(entry => entry.TaskId));
        Assert.Empty(onTomorrow.ComingUp);
    }

    [Fact]
    public void AnUndatedTasksTimeIsIgnoredWhenAStepBringsItToToday()
    {
        var task = Timed(Due(Today), 9, 30);
        var step = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, step, "Call"), Now));
        task.ApplyAll(TodoTask.Decide(task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, step, Today), Now));
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, null), Now));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Empty(plan.Scheduled);
        Assert.Null(Assert.Single(plan.AnyTime).Time);
    }

    static TodoTask Due(DateOnly day)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), Now));
        return task;
    }

    static TodoTask Timed(TodoTask task, int hour, int minute)
    {
        task.ApplyAll(TodoTask.Decide(task,
            new SetTaskTime(Guid.NewGuid(), User, task.Id, TaskTime.Of(new TimeOnly(hour, minute), null)), Now));
        return task;
    }

    static TodoTask Star(TodoTask task)
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
