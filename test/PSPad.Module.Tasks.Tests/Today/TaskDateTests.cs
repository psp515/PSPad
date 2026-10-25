using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;
using static PSPad.Module.Tasks.Tests.Lists.View.ViewTasks;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class TaskDateTests
{
    [Fact]
    public void AOneTimeTasksDateIsItsDueDate() =>
        Assert.Equal(TodayDate.AddDays(3), TodayRule.DateOf(Due(TodayDate.AddDays(3)), TodayDate));

    [Fact]
    public void AnEarlierStepDeadlineWins() =>
        Assert.Equal(TodayDate.AddDays(1), TodayRule.DateOf(WithStep(Due(TodayDate.AddDays(5)), TodayDate.AddDays(1)), TodayDate));

    [Fact]
    public void ACheckedStepsDeadlineIsIgnored() =>
        Assert.Equal(TodayDate.AddDays(5), TodayRule.DateOf(WithStep(Due(TodayDate.AddDays(5)), TodayDate.AddDays(1), @checked: true), TodayDate));

    [Fact]
    public void AStepDeadlineDatesAnUndatedTask() =>
        Assert.Equal(TodayDate.AddDays(2), TodayRule.DateOf(WithStep(Undated(), TodayDate.AddDays(2)), TodayDate));

    [Fact]
    public void AnUndatedTaskHasNoDate() => Assert.Null(TodayRule.DateOf(Undated(), TodayDate));

    [Fact]
    public void APastOneTimeDateStaysPast() =>
        Assert.Equal(TodayDate.AddDays(-3), TodayRule.DateOf(Due(TodayDate.AddDays(-3)), TodayDate));

    [Fact]
    public void ARecurringTasksDateIsTodayWhenTodayIsUnticked() =>
        Assert.Equal(TodayDate, TodayRule.DateOf(Recurring(RecurrenceRule.Daily(TodayDate.AddDays(-10))), TodayDate));

    [Fact]
    public void ATickedTodayMovesARecurringTaskToItsNextOccurrence() =>
        Assert.Equal(TodayDate.AddDays(1), TodayRule.DateOf(Ticked(Recurring(RecurrenceRule.Daily(TodayDate.AddDays(-10))), TodayDate), TodayDate));

    [Fact]
    public void ARecurringTaskIsNeverBeforeToday()
    {
        var weekly = Recurring(RecurrenceRule.Weekly(TodayDate.AddDays(-14), TodayDate.AddDays(-1).DayOfWeek));

        Assert.Equal(TodayDate.AddDays(6), TodayRule.DateOf(weekly, TodayDate));
    }

    [Fact]
    public void AnEndedRepeatHasNoDate() =>
        Assert.Null(TodayRule.DateOf(EndedOn(Recurring(RecurrenceRule.Daily(TodayDate.AddDays(-10))), TodayDate.AddDays(-1)), TodayDate));

    [Fact]
    public void ARepeatBeyondTheHorizonHasNoDate() =>
        Assert.Null(TodayRule.DateOf(Recurring(RecurrenceRule.Yearly(TodayDate.AddDays(1).AddYears(-1)).EveryNth(3)), TodayDate));

    [Fact]
    public void TriggerOfIgnoresRecurrence() =>
        Assert.Equal(TodayDate.AddDays(4), TodayRule.TriggerOf(Due(TodayDate.AddDays(4))));
}
