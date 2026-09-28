using PSPad.Abstractions;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TaskRecurrenceTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ATaskWithARuleIsRecurring()
    {
        var task = Recurring();

        Assert.True(task.IsRecurring);
        Assert.NotNull(task.Recurrence);
    }

    [Fact]
    public void ARecurringTaskTakesADueDateAsItsEnd()
    {
        var task = Recurring();

        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today), Now));

        Assert.Equal(Today, task.DueOn);
        Assert.True(task.OccursOn(Today));
        Assert.False(task.OccursOn(Today.AddDays(1)));
    }

    [Fact]
    public void ADatedTaskCanStartRepeating()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today), Now));

        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(new DateOnly(2026, 9, 1))),
            Now));

        Assert.True(task.IsRecurring);
        Assert.Equal(Today, task.DueOn);
    }

    [Fact]
    public void TickingADayAfterTheEndIsRejected()
    {
        var task = EndingOn(Today);

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today.AddDays(1), true), Now));
    }

    [Fact]
    public void TickingTheEndDayItselfIsAccepted()
    {
        var task = EndingOn(Today);

        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        Assert.Contains(Today, task.CompletedDays);
    }

    [Fact]
    public void ARecurringTaskEndsOnceItsEndDayHasPassed()
    {
        var task = EndingOn(Today);

        Assert.False(task.EndedBy(Today));
        Assert.True(task.EndedBy(Today.AddDays(1)));
    }

    [Fact]
    public void ARecurringTaskWithoutAnEndNeverEnds()
    {
        Assert.False(Recurring().EndedBy(Today.AddYears(10)));
    }

    [Fact]
    public void APlainTaskPastItsDueDateIsNotEnded()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today), Now));

        Assert.False(task.EndedBy(Today.AddDays(1)));
        Assert.False(task.OccursOn(Today));
    }

    [Fact]
    public void CompletingAnOccurrenceRecordsThatDayOnly()
    {
        var task = Recurring();

        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        Assert.Contains(Today, task.CompletedDays);
        Assert.DoesNotContain(Today.AddDays(-1), task.CompletedDays);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void UncompletingAnOccurrenceTakesTheDayBackOut()
    {
        var task = Recurring();
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, false), Now));

        Assert.Empty(task.CompletedDays);
    }

    [Fact]
    public void CompletingADayTheRuleDoesNotCoverIsRejected()
    {
        var task = Recurring(RecurrenceRule.Weekly(new DateOnly(2026, 9, 7), DayOfWeek.Monday));

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, new DateOnly(2026, 9, 12), true), Now));
    }

    [Fact]
    public void CompletingTheWholeTaskIsRejectedWhileItRecurs()
    {
        var task = Recurring();

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));
    }

    [Fact]
    public void ClearingTheRuleLeavesAPlainTask()
    {
        var task = Recurring();

        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, null), Now));

        Assert.False(task.IsRecurring);
    }

    internal static TodoTask Recurring(RecurrenceRule? rule = null)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(
                Guid.NewGuid(), User, task.Id, rule ?? RecurrenceRule.Daily(new DateOnly(2026, 9, 1))),
            Now));
        return task;
    }

    static TodoTask EndingOn(DateOnly day)
    {
        var task = Recurring();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), Now));
        return task;
    }
}
