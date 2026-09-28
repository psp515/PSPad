using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class GoalPaceTests
{
    static readonly DateOnly Start = new(2026, 9, 7);

    [Fact]
    public void NothingToCountHasNoPace()
    {
        Assert.Null(GoalPace.Of([], new DateOnly(2026, 10, 7), new DateOnly(2026, 9, 22)));
    }

    [Fact]
    public void HalfwayThroughTheTimeUsesHalfOfIt()
    {
        var pace = GoalPace.Of([Week(Start, 4, 2)], new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 17))!;

        Assert.Equal(50, pace.TimePercent);
        Assert.Equal(10, pace.DaysLeft);
    }

    [Fact]
    public void WorkIsTheShareOfTasksDoneByTheLatestWeek()
    {
        var pace = GoalPace.Of([Week(Start, 2, 0), Week(Start.AddDays(7), 5, 2)], new DateOnly(2026, 10, 7), new DateOnly(2026, 9, 17))!;

        Assert.Equal(40, pace.WorkPercent);
    }

    [Fact]
    public void WorkBehindTimeIsBehind()
    {
        var behind = GoalPace.Of([Week(Start, 10, 3)], new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 17))!;
        var ahead = GoalPace.Of([Week(Start, 10, 7)], new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 17))!;

        Assert.True(behind.Behind);
        Assert.False(ahead.Behind);
    }

    [Fact]
    public void PastTheDueDateTimeIsFullAndDaysLeftGoesNegative()
    {
        var pace = GoalPace.Of([Week(Start, 4, 4)], new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 23))!;

        Assert.Equal(100, pace.TimePercent);
        Assert.Equal(-3, pace.DaysLeft);
        Assert.False(pace.Behind);
    }

    [Fact]
    public void ADueDateBeforeTheFirstTaskCountsAllTimeAsUsed()
    {
        var pace = GoalPace.Of([Week(Start, 4, 1)], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10))!;

        Assert.Equal(100, pace.TimePercent);
        Assert.True(pace.Behind);
    }

    static GoalProgressWeek Week(DateOnly start, int total, int done) => new(start, total, done);
}
