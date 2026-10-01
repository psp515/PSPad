using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class LeadTimePlanTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void WithoutALeadTimeUpcomingStillReachesOneWeek()
    {
        var inside = Due(Today.AddDays(7));
        var beyond = Due(Today.AddDays(8));

        var plan = TodayRule.Plan([inside, beyond], Today, Utc);

        Assert.Equal(inside.Id, Assert.Single(plan.Upcoming).TaskId);
    }

    [Fact]
    public void AOneDayLeadHidesATaskDueInThreeDays()
    {
        var task = Lead(Due(Today.AddDays(3)), LeadTime.Of(1, LeadUnit.Days));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Upcoming);
        Assert.Empty(plan.Tomorrow);
    }

    [Fact]
    public void AOneDayLeadShowsTheTaskTheDayBefore()
    {
        var task = Lead(Due(Today.AddDays(1)), LeadTime.Of(1, LeadUnit.Days));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Equal(task.Id, Assert.Single(plan.Tomorrow).TaskId);
    }

    [Fact]
    public void AOneMonthLeadShowsATaskDueInThreeWeeks()
    {
        var task = Lead(Due(Today.AddDays(20)), LeadTime.Of(1, LeadUnit.Months));

        var plan = TodayRule.Plan([task], Today, Utc);

        var entry = Assert.Single(plan.Upcoming);
        Assert.Equal(Today.AddDays(20), entry.DueOn);
    }

    [Fact]
    public void ALeadTimeNeverPutsATaskOnTodayEarly()
    {
        var task = Lead(Due(Today.AddDays(5)), LeadTime.Of(2, LeadUnit.Weeks));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Today);
        Assert.Empty(plan.Overdue);
        Assert.Single(plan.Upcoming);
    }

    [Fact]
    public void ALeadTimeLeavesAnOverdueTaskOverdue()
    {
        var task = Lead(Due(Today.AddDays(-1)), LeadTime.Of(1, LeadUnit.Days));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Single(plan.Overdue);
    }

    [Fact]
    public void AYearlyRepeatWithAMonthsLeadShowsItsNextOccurrenceThirtyDaysOut()
    {
        var task = Lead(
            Recurring(RecurrenceRule.Yearly(new DateOnly(2025, 10, 12))), LeadTime.Of(1, LeadUnit.Months));

        var plan = TodayRule.Plan([task], Today, Utc);

        var entry = Assert.Single(plan.Upcoming);
        Assert.Equal(new DateOnly(2026, 10, 12), entry.DueOn);
        Assert.True(entry.Recurring);
    }

    [Fact]
    public void AYearlyRepeatWithAMonthsLeadStaysHiddenFortyDaysOut()
    {
        var task = Lead(
            Recurring(RecurrenceRule.Yearly(new DateOnly(2025, 10, 22))), LeadTime.Of(1, LeadUnit.Months));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Upcoming);
        Assert.Empty(plan.Tomorrow);
    }

    [Fact]
    public void ADailyRepeatWithADayLeadShowsOnlyTomorrowsOccurrence()
    {
        var task = Lead(Recurring(RecurrenceRule.Daily(Today)), LeadTime.Of(1, LeadUnit.Days));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Equal(Today.AddDays(1), Assert.Single(plan.Tomorrow).DueOn);
        Assert.Empty(plan.Upcoming);
    }

    [Fact]
    public void AWeeklyRepeatWithADayLeadHidesAnOccurrenceFourDaysOut()
    {
        var task = Lead(
            Recurring(RecurrenceRule.Weekly(Today, Today.AddDays(4).DayOfWeek)), LeadTime.Of(1, LeadUnit.Days));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Tomorrow);
        Assert.Empty(plan.Upcoming);
    }

    [Fact]
    public void ADailyRepeatWithTomorrowTickedAndAThreeDayLeadShowsTheDayAfterInUpcoming()
    {
        var task = Lead(Recurring(RecurrenceRule.Daily(Today)), LeadTime.Of(3, LeadUnit.Days));
        TickOccurrence(task, Today.AddDays(1));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Tomorrow);
        Assert.Equal(Today.AddDays(2), Assert.Single(plan.Upcoming).DueOn);
    }

    [Fact]
    public void ADailyRepeatWithTomorrowTickedAndADayLeadShowsNothingAhead()
    {
        var task = Lead(Recurring(RecurrenceRule.Daily(Today)), LeadTime.Of(1, LeadUnit.Days));
        TickOccurrence(task, Today.AddDays(1));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Tomorrow);
        Assert.Empty(plan.Upcoming);
    }

    [Fact]
    public void AMonthsLeadShowsAStepDueInTwentyDaysUsingTheStepDate()
    {
        var task = WithStepDue(Today.AddDays(20));
        Lead(task, LeadTime.Of(1, LeadUnit.Months));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Equal(Today.AddDays(20), Assert.Single(plan.Upcoming).DueOn);
    }

    [Fact]
    public void WithoutALeadAStepDueInTwentyDaysIsNotShown()
    {
        var task = WithStepDue(Today.AddDays(20));

        var plan = TodayRule.Plan([task], Today, Utc);

        Assert.Empty(plan.Upcoming);
        Assert.Empty(plan.Tomorrow);
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

    static TodoTask Lead(TodoTask task, LeadTime lead)
    {
        task.ApplyAll(TodoTask.Decide(task, new SetTaskLeadTime(Guid.NewGuid(), User, task.Id, lead), Now));
        return task;
    }
}
