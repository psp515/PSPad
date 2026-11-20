using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class ComingUpLookAheadTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void ATaskDueInFiveDaysIsComingUpButNeverOnTodayEarly()
    {
        var task = Due(Today.AddDays(5));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Empty(plan.AnyTime);
        Assert.Empty(plan.Overdue);
        Assert.Equal(task.Id, Assert.Single(plan.ComingUp).TaskId);
    }

    [Fact]
    public void AYearlyRepeatThirtyDaysOutIsNotComingUp()
    {
        var task = Recurring(RecurrenceRule.Yearly(new DateOnly(2025, 10, 12)));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Empty(plan.ComingUp);
    }

    [Fact]
    public void AYearlyRepeatSixDaysOutIsComingUp()
    {
        var task = Recurring(RecurrenceRule.Yearly(new DateOnly(2025, 9, 18)));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        var entry = Assert.Single(plan.ComingUp);
        Assert.Equal(new DateOnly(2026, 9, 18), entry.DueOn);
        Assert.True(entry.Recurring);
    }

    [Fact]
    public void ADailyRepeatShowsOnlyTomorrowsOccurrence()
    {
        var task = Recurring(RecurrenceRule.Daily(Today));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Equal(Today.AddDays(1), Assert.Single(plan.ComingUp).DueOn);
    }

    [Fact]
    public void ADailyRepeatWithTomorrowTickedShowsTheDayAfter()
    {
        var task = Recurring(RecurrenceRule.Daily(Today));
        TickOccurrence(task, Today.AddDays(1));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Equal(Today.AddDays(2), Assert.Single(plan.ComingUp).DueOn);
    }

    [Fact]
    public void AStepDueInFiveDaysShowsItsTaskOnTheStepDate()
    {
        var task = WithStepDue(Today.AddDays(5));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Equal(Today.AddDays(5), Assert.Single(plan.ComingUp).DueOn);
    }

    [Fact]
    public void AStepDueInTwentyDaysIsNotShown()
    {
        var task = WithStepDue(Today.AddDays(20));

        var plan = TodayRule.Plan([task], Today, Today, Utc);

        Assert.Empty(plan.ComingUp);
    }

    static void TickOccurrence(TodoTask task, DateOnly day) =>
        task.ApplyAll(TodoTask.Decide(task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, day, true), Now));

    static TodoTask WithStepDue(DateOnly day)
    {
        var task = TodoTaskTests.Existing();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "call"), Now));
        task.ApplyAll(TodoTask.Decide(task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, day), Now));
        return task;
    }

    static TodoTask Due(DateOnly day)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), Now));
        return task;
    }

    static TodoTask Recurring(RecurrenceRule rule)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule), Now));
        return task;
    }
}
