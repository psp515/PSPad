using PSPad.App.State;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class GoalProgressTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public void AGoalWithNoTasksHasNoWeeks()
    {
        Assert.Empty(GoalProgress.Of([], "Etc/UTC", Today));
    }

    [Fact]
    public void WeeksRunFromTheFirstTasksMondayToThisWeek()
    {
        var task = Created(At(2026, 9, 10));

        var weeks = GoalProgress.Of([task], "Etc/UTC", Today);

        Assert.Equal(
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28)],
            weeks.Select(week => week.WeekStart));
    }

    [Fact]
    public void EachWeekCountsTasksCreatedAndDoneByItsEnd()
    {
        var early = Complete(Created(At(2026, 9, 8)), At(2026, 9, 20));
        var late = Created(At(2026, 9, 15));

        var weeks = GoalProgress.Of([early, late], "Etc/UTC", Today);

        Assert.Equal([1, 2, 2, 2], weeks.Select(week => week.Total));
        Assert.Equal([0, 1, 1, 1], weeks.Select(week => week.Done));
    }

    [Fact]
    public void ASundayTaskBelongsToTheWeekThatEndsThatDay()
    {
        var task = Created(At(2026, 9, 13, 23));

        var weeks = GoalProgress.Of([task], "Etc/UTC", Today);

        Assert.Equal(new DateOnly(2026, 9, 7), weeks[0].WeekStart);
        Assert.Equal(1, weeks[0].Total);
    }

    [Fact]
    public void DaysAreTakenInTheUsersTimeZone()
    {
        var task = Created(At(2026, 9, 14, 2));

        var utc = GoalProgress.Of([task], "Etc/UTC", Today);
        var newYork = GoalProgress.Of([task], "America/New_York", Today);

        Assert.Equal(new DateOnly(2026, 9, 14), utc[0].WeekStart);
        Assert.Equal(new DateOnly(2026, 9, 7), newYork[0].WeekStart);
    }

    [Fact]
    public void RecurringAndDeletedTasksAreNotCounted()
    {
        var recurring = Created(At(2026, 9, 1));
        recurring.ApplyAll(TodoTask.Decide(
            recurring, new SetTaskRecurrence(Guid.NewGuid(), User, recurring.Id, RecurrenceRule.Daily(new DateOnly(2026, 9, 1))), At(2026, 9, 1)));
        var deleted = Created(At(2026, 9, 2));
        deleted.ApplyAll(TodoTask.Decide(deleted, new DeleteTask(Guid.NewGuid(), User, deleted.Id), At(2026, 9, 2)));
        var counted = Created(At(2026, 9, 22));

        var weeks = GoalProgress.Of([recurring, deleted, counted], "Etc/UTC", Today);

        Assert.Equal([new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28)], weeks.Select(week => week.WeekStart));
        Assert.All(weeks, week => Assert.Equal(1, week.Total));
    }

    static DateTimeOffset At(int year, int month, int day, int hour = 12) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    static TodoTask Created(DateTimeOffset at)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Task"), at));
        return task;
    }

    static TodoTask Complete(TodoTask task, DateTimeOffset at)
    {
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), at));
        return task;
    }
}
