using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;
using static PSPad.Module.Tasks.Tests.Lists.View.ViewTasks;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class TaskDateTests
{
    static readonly DateOnly Today = Lists.View.ViewTasks.Today;

    [Fact]
    public void AOneTimeTasksDateIsItsDueDate() =>
        Assert.Equal(Today.AddDays(3), TodayRule.DateOf(Due(Today.AddDays(3)), Today));

    [Fact]
    public void AnEarlierStepDeadlineWins() =>
        Assert.Equal(Today.AddDays(1), TodayRule.DateOf(WithStep(Due(Today.AddDays(5)), Today.AddDays(1)), Today));

    [Fact]
    public void ACheckedStepsDeadlineIsIgnored() =>
        Assert.Equal(Today.AddDays(5), TodayRule.DateOf(WithStep(Due(Today.AddDays(5)), Today.AddDays(1), @checked: true), Today));

    [Fact]
    public void AStepDeadlineDatesAnUndatedTask() =>
        Assert.Equal(Today.AddDays(2), TodayRule.DateOf(WithStep(Undated(), Today.AddDays(2)), Today));

    [Fact]
    public void AnUndatedTaskHasNoDate() => Assert.Null(TodayRule.DateOf(Undated(), Today));

    [Fact]
    public void APastOneTimeDateStaysPast() =>
        Assert.Equal(Today.AddDays(-3), TodayRule.DateOf(Due(Today.AddDays(-3)), Today));

    [Fact]
    public void ARecurringTasksDateIsTodayWhenTodayIsUnticked() =>
        Assert.Equal(Today, TodayRule.DateOf(Recurring(RecurrenceRule.Daily(Today.AddDays(-10))), Today));

    [Fact]
    public void ATickedTodayMovesARecurringTaskToItsNextOccurrence() =>
        Assert.Equal(Today.AddDays(1), TodayRule.DateOf(Ticked(Recurring(RecurrenceRule.Daily(Today.AddDays(-10))), Today), Today));

    [Fact]
    public void ARecurringTaskIsNeverBeforeToday()
    {
        var weekly = Recurring(RecurrenceRule.Weekly(Today.AddDays(-14), Today.AddDays(-1).DayOfWeek));

        Assert.Equal(Today.AddDays(6), TodayRule.DateOf(weekly, Today));
    }

    [Fact]
    public void AnEndedRepeatHasNoDate() =>
        Assert.Null(TodayRule.DateOf(EndedOn(Recurring(RecurrenceRule.Daily(Today.AddDays(-10))), Today.AddDays(-1)), Today));

    [Fact]
    public void ARepeatBeyondTheHorizonHasNoDate() =>
        Assert.Null(TodayRule.DateOf(Recurring(RecurrenceRule.Yearly(Today.AddDays(1).AddYears(-1)).EveryNth(3)), Today));

    [Fact]
    public void TriggerOfIgnoresRecurrence() =>
        Assert.Equal(Today.AddDays(4), TodayRule.TriggerOf(Due(Today.AddDays(4))));
}
