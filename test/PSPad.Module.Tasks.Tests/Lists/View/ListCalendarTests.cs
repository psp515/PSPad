using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Module.Tasks.Tests.Lists.View.ViewTasks;

namespace PSPad.Module.Tasks.Tests.Lists.View;

[UnitTest]
public class ListCalendarTests
{
    static IReadOnlyList<CalendarDay> October(params TodoTask[] tasks) =>
        ListCalendar.Month(tasks, 2026, 10, TodayDate);

    [Fact]
    public void AOneTimeTaskSitsOnItsDate()
    {
        var task = Due(new DateOnly(2026, 10, 27));

        var day = Assert.Single(October(task));
        Assert.Equal(new DateOnly(2026, 10, 27), day.Day);
        Assert.Equal(task.Id, Assert.Single(day.Entries).TaskId);
    }

    [Fact]
    public void AStepDeadlinePlacesTheTask()
    {
        var task = WithStep(Due(new DateOnly(2026, 10, 31)), new DateOnly(2026, 10, 24));

        Assert.Equal(new DateOnly(2026, 10, 24), Assert.Single(October(task)).Day);
    }

    [Fact]
    public void APastOneTimeTaskIsOverdueOnItsDay()
    {
        var entry = Assert.Single(Assert.Single(October(Due(new DateOnly(2026, 10, 20)))).Entries);

        Assert.True(entry.Overdue);
    }

    [Fact]
    public void ARecurringTaskFillsOnlyItsDaysFromToday()
    {
        var mondays = Recurring(RecurrenceRule.Weekly(new DateOnly(2026, 9, 1), DayOfWeek.Monday));

        Assert.Equal([new DateOnly(2026, 10, 26)], October(mondays).Select(day => day.Day));
        Assert.All(October(mondays).SelectMany(day => day.Entries), entry => Assert.True(entry.Recurring));
    }

    [Fact]
    public void ATickedOccurrenceIsLeftOut()
    {
        var daily = Ticked(Recurring(RecurrenceRule.Daily(new DateOnly(2026, 9, 1))), TodayDate);

        Assert.DoesNotContain(October(daily), day => day.Day == TodayDate);
    }

    [Fact]
    public void AnotherMonthsTaskIsNotPlaced() =>
        Assert.Empty(October(Due(new DateOnly(2026, 11, 2))));

    [Fact]
    public void CompletedAndUndatedTasksAreLeftOut() =>
        Assert.Empty(October(Completed(Due(new DateOnly(2026, 10, 27))), Undated()));

    [Fact]
    public void APastMonthHoldsOnlyOverdueOneTimeTasks()
    {
        var daily = Recurring(RecurrenceRule.Daily(new DateOnly(2026, 9, 1)));
        var late = Due(new DateOnly(2026, 9, 15));

        var september = ListCalendar.Month([daily, late], 2026, 9, TodayDate);

        Assert.Equal([new DateOnly(2026, 9, 15)], september.Select(day => day.Day));
    }

    [Fact]
    public void ALeapFebruaryHasItsTwentyNinth()
    {
        var task = Due(new DateOnly(2028, 2, 29));

        Assert.Equal(new DateOnly(2028, 2, 29), Assert.Single(ListCalendar.Month([task], 2028, 2, TodayDate)).Day);
    }

    [Fact]
    public void TwoTasksOnOneDayShareIt() =>
        Assert.Equal(2, Assert.Single(October(Due(new DateOnly(2026, 10, 27)), Due(new DateOnly(2026, 10, 27)))).Entries.Count);
}
