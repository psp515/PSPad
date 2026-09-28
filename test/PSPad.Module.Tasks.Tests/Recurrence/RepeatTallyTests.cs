using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Recurrence;

[UnitTest]
public class RepeatTallyTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ANeverTickedTaskHasNothingToCount()
    {
        var task = TaskRecurrenceTests.Recurring(RecurrenceRule.Daily(Today.AddDays(-5)));

        Assert.Equal(new RepeatTally(0, 0), RepeatTally.Of(task, Today));
    }

    [Fact]
    public void TheTotalCountsEveryTickedDay()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-10)), -9, -5, -1);

        Assert.Equal(3, RepeatTally.Of(task, Today).Total);
    }

    [Fact]
    public void TheStreakStopsAtTheFirstSkippedOccurrence()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-10)), -5, -3, -2, -1);

        Assert.Equal(new RepeatTally(4, 3), RepeatTally.Of(task, Today));
    }

    [Fact]
    public void TodayStillPendingDoesNotBreakTheStreak()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-10)), -2, -1);

        Assert.Equal(2, RepeatTally.Of(task, Today).Streak);
    }

    [Fact]
    public void TodayTickedExtendsTheStreak()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-10)), -2, -1, 0);

        Assert.Equal(3, RepeatTally.Of(task, Today).Streak);
    }

    [Fact]
    public void AMissedYesterdayBreaksTheStreak()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-10)), -3, -2);

        Assert.Equal(0, RepeatTally.Of(task, Today).Streak);
    }

    [Fact]
    public void TheStreakOnlyWalksOccurrenceDays()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-9)).EveryNth(3), -9, -6, -3);

        Assert.Equal(new RepeatTally(3, 3), RepeatTally.Of(task, Today));
    }

    [Fact]
    public void TheStreakStopsAtTheStart()
    {
        var task = Ticked(RecurrenceRule.Daily(Today.AddDays(-2)), -2, -1, 0);

        Assert.Equal(3, RepeatTally.Of(task, Today).Streak);
    }

    static TodoTask Ticked(RecurrenceRule rule, params int[] offsets)
    {
        var task = TaskRecurrenceTests.Recurring(rule);
        foreach (var offset in offsets)
        {
            task.ApplyAll(TodoTask.Decide(
                task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today.AddDays(offset), true), Now));
        }

        return task;
    }
}
