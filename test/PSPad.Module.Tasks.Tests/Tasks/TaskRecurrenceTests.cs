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
    public void ARecurringTaskCannotCarryADueDate()
    {
        var task = Recurring();

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today), Now));
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
}
