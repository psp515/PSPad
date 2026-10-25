using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Module.Tasks.Tests.Lists.View.ViewTasks;

namespace PSPad.Module.Tasks.Tests.Lists.View;

[UnitTest]
public class ListFilterTests
{
    static readonly DayOfWeek Monday = DayOfWeek.Monday;

    static ListFilter When(params WhenBucket[] buckets) =>
        ListFilter.None with { When = buckets.ToHashSet() };

    [Theory]
    [InlineData(-1, WhenBucket.Overdue)]
    [InlineData(0, WhenBucket.Today)]
    [InlineData(1, WhenBucket.ThisWeek)]
    [InlineData(2, WhenBucket.ThisWeek)]
    [InlineData(3, WhenBucket.Later)]
    public void EachDateFallsInOneBucketWithAMondayWeek(int offset, WhenBucket expected) =>
        Assert.Equal(expected, ListFilter.BucketOf(Due(TodayDate.AddDays(offset)), TodayDate, Monday));

    [Fact]
    public void ASundayWeekEndsOnSaturday() =>
        Assert.Equal(WhenBucket.ThisWeek, ListFilter.BucketOf(Due(TodayDate.AddDays(1)), TodayDate, DayOfWeek.Sunday));

    [Fact]
    public void OnTheWeeksLastDayTomorrowIsLater()
    {
        var sunday = new DateOnly(2026, 10, 25);

        Assert.Equal(WhenBucket.Later, ListFilter.BucketOf(Due(sunday.AddDays(1)), sunday, Monday));
    }

    [Fact]
    public void AnUndatedTaskIsNoDate() =>
        Assert.Equal(WhenBucket.NoDate, ListFilter.BucketOf(Undated(), TodayDate, Monday));

    [Fact]
    public void NoFilterMatchesEverything() =>
        Assert.True(ListFilter.None.Matches(Undated(), TodayDate, Monday, []));

    [Fact]
    public void ChoicesInsideAGroupAreOred()
    {
        var filter = When(WhenBucket.Overdue, WhenBucket.Today);

        Assert.True(filter.Matches(Due(TodayDate.AddDays(-2)), TodayDate, Monday, []));
        Assert.True(filter.Matches(Due(TodayDate), TodayDate, Monday, []));
        Assert.False(filter.Matches(Due(TodayDate.AddDays(9)), TodayDate, Monday, []));
    }

    [Fact]
    public void GroupsAreAnded()
    {
        var filter = When(WhenBucket.Today) with { Priorities = new HashSet<Priority> { Priority.High } };

        Assert.True(filter.Matches(Prioritised(Due(TodayDate), Priority.High), TodayDate, Monday, []));
        Assert.False(filter.Matches(Due(TodayDate), TodayDate, Monday, []));
        Assert.False(filter.Matches(Prioritised(Undated(), Priority.High), TodayDate, Monday, []));
    }

    [Fact]
    public void OtherOrsStarredHasAGoalAndEachGoal()
    {
        var starred = Starred(Undated("starred"));
        var linked = Undated("linked");
        var plain = Undated("plain");
        var goal = GoalHolding("Move", linked);
        var filter = ListFilter.None with { Starred = true, HasGoal = true };

        Assert.True(filter.Matches(starred, TodayDate, Monday, [goal]));
        Assert.True(filter.Matches(linked, TodayDate, Monday, [goal]));
        Assert.False(filter.Matches(plain, TodayDate, Monday, [goal]));
    }

    [Fact]
    public void ASpecificGoalMatchesOnlyItsTasks()
    {
        var mine = Undated("mine");
        var other = Undated("other");
        var move = GoalHolding("Move", mine);
        var learn = GoalHolding("Learn", other);
        var filter = ListFilter.None with { GoalIds = new HashSet<Guid> { move.Id } };

        Assert.True(filter.Matches(mine, TodayDate, Monday, [move, learn]));
        Assert.False(filter.Matches(other, TodayDate, Monday, [move, learn]));
    }

    [Fact]
    public void ADeletedGoalHoldsNothing()
    {
        var task = Undated();
        var goal = GoalHolding("Gone", task);
        goal.Apply(new GoalDeleted(goal.Id, User, Now));

        Assert.False((ListFilter.None with { HasGoal = true }).Matches(task, TodayDate, Monday, [goal]));
    }

    [Fact]
    public void ACompletedTaskIsNeverOverdue() =>
        Assert.False(When(WhenBucket.Overdue).Matches(Completed(Due(TodayDate.AddDays(-2))), TodayDate, Monday, []));

    [Fact]
    public void ACompletedTaskStillMatchesItsDateBucket() =>
        Assert.True(When(WhenBucket.Later).Matches(Completed(Due(TodayDate.AddDays(20))), TodayDate, Monday, []));

    [Fact]
    public void ActiveCountCountsEveryChoice()
    {
        var filter = When(WhenBucket.Today, WhenBucket.ThisWeek) with
        {
            Priorities = new HashSet<Priority> { Priority.High },
            Starred = true,
            GoalIds = new HashSet<Guid> { Guid.NewGuid() }
        };

        Assert.Equal(5, filter.ActiveCount);
        Assert.True(ListFilter.None.IsEmpty);
        Assert.False(filter.IsEmpty);
    }
}
