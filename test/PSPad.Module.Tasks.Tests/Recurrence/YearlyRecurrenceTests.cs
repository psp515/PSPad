using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Recurrence;

[UnitTest]
public class YearlyRecurrenceTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void AYearlyRuleOccursOnItsDayEachYear()
    {
        var rule = RecurrenceRule.Yearly(new DateOnly(2026, 3, 15));

        Assert.True(rule.OccursOn(new DateOnly(2026, 3, 15)));
        Assert.True(rule.OccursOn(new DateOnly(2027, 3, 15)));
        Assert.False(rule.OccursOn(new DateOnly(2027, 3, 14)));
        Assert.False(rule.OccursOn(new DateOnly(2027, 4, 15)));
        Assert.False(rule.OccursOn(new DateOnly(2025, 3, 15)));
    }

    [Fact]
    public void EveryTwoYearsSkipsTheYearsBetween()
    {
        var rule = RecurrenceRule.Yearly(new DateOnly(2026, 3, 15)).EveryNth(2);

        Assert.False(rule.OccursOn(new DateOnly(2027, 3, 15)));
        Assert.True(rule.OccursOn(new DateOnly(2028, 3, 15)));
    }

    [Fact]
    public void ALeapDayFallsOnTheTwentyEighthInOtherYears()
    {
        var rule = RecurrenceRule.Yearly(new DateOnly(2024, 2, 29));

        Assert.True(rule.OccursOn(new DateOnly(2027, 2, 28)));
        Assert.True(rule.OccursOn(new DateOnly(2028, 2, 29)));
        Assert.False(rule.OccursOn(new DateOnly(2028, 2, 28)));
        Assert.False(rule.OccursOn(new DateOnly(2027, 3, 1)));
    }

    [Fact]
    public void AYearlyTaskStopsAtItsEnd()
    {
        var task = TodoTaskTests.Existing();
        var at = DateTimeOffset.UnixEpoch;
        task.ApplyAll(TodoTask.Decide(task, new SetTaskRecurrence(
            Guid.NewGuid(), User, task.Id, RecurrenceRule.Yearly(new DateOnly(2026, 3, 15))), at));
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(
            Guid.NewGuid(), User, task.Id, new DateOnly(2027, 12, 31)), at));

        Assert.True(task.OccursOn(new DateOnly(2027, 3, 15)));
        Assert.False(task.OccursOn(new DateOnly(2028, 3, 15)));
    }
}
