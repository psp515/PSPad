using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class TodayRuleTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void TodayIsReadInTheUsersZoneNotUtc()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        var lateUtc = new DateTimeOffset(2026, 9, 12, 23, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 13), TodayRule.TodayIn(lateUtc, zone));
    }

    [Fact]
    public void ATaskDueTodayIsOnToday()
    {
        var task = Due(Today);

        var entry = Assert.Single(TodayRule.Select([task], Today));

        Assert.Equal(task.Id, entry.TaskId);
        Assert.False(entry.Overdue);
    }

    [Fact]
    public void ATaskDueEarlierIsOverdue()
    {
        var entry = Assert.Single(TodayRule.Select([Due(Today.AddDays(-3))], Today));

        Assert.True(entry.Overdue);
    }

    [Fact]
    public void ATaskDueLaterIsNotOnToday()
    {
        Assert.Empty(TodayRule.Select([Due(Today.AddDays(1))], Today));
    }

    [Fact]
    public void ATaskWhoseNextUncheckedStepIsDueTodayIsOnToday()
    {
        var task = TodoTaskTests.Existing();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "call"), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, Today), Now));

        Assert.Single(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ACheckedStepStopsDrivingToday()
    {
        var task = TodoTaskTests.Existing();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "call"), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, Today), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, stepId, true), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void AStarNeverPutsATaskOnToday()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new StarTask(Guid.NewGuid(), User, task.Id, true), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ACompletedTaskDropsOffToday()
    {
        var task = Due(Today);
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ARecurringTaskShowsOnlyTodaysPendingOccurrence()
    {
        var task = DailyFrom(Today.AddDays(-5));

        var entry = Assert.Single(TodayRule.Select([task], Today));

        Assert.False(entry.Overdue);
    }

    [Fact]
    public void ARecurringTaskMissedYesterdayIsStillNotOverdueToday()
    {
        var task = DailyFrom(Today.AddDays(-5));

        var entry = Assert.Single(TodayRule.Select([task], Today));

        Assert.False(entry.Overdue);
        Assert.Equal(OccurrenceStatus.Skipped,
            Occurrences.Between(task, Today.AddDays(-1), Today.AddDays(-1), Today).Single().Status);
    }

    [Fact]
    public void ARecurringTaskTickedTodayDropsOffToday()
    {
        var task = DailyFrom(Today.AddDays(-5));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ARecurringTaskThatDoesNotFallTodayIsNotOnToday()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(
                Guid.NewGuid(), User, task.Id,
                RecurrenceRule.Weekly(Today.AddDays(-7), DayOfWeek.Monday)),
            Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void OverdueTasksSortAboveTheRest()
    {
        var overdue = Due(Today.AddDays(-1));
        var dueToday = Due(Today);

        var entries = TodayRule.Select([dueToday, overdue], Today);

        Assert.Equal(overdue.Id, entries[0].TaskId);
        Assert.Equal(dueToday.Id, entries[1].TaskId);
    }

    [Fact]
    public void WithinTheSameUrgencyStarsThenPrioritySortUp()
    {
        var plain = Due(Today);
        var important = Due(Today);
        important.ApplyAll(TodoTask.Decide(
            important, new StarTask(Guid.NewGuid(), User, important.Id, true), Now));
        var urgent = Due(Today);
        urgent.ApplyAll(TodoTask.Decide(
            urgent, new SetTaskPriority(Guid.NewGuid(), User, urgent.Id, Priority.High), Now));

        var entries = TodayRule.Select([plain, urgent, important], Today);

        Assert.Equal(important.Id, entries[0].TaskId);
        Assert.Equal(urgent.Id, entries[1].TaskId);
        Assert.Equal(plain.Id, entries[2].TaskId);
    }

    static TodoTask Due(DateOnly day)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), Now));
        return task;
    }

    static TodoTask DailyFrom(DateOnly start)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(start)), Now));
        return task;
    }
}
