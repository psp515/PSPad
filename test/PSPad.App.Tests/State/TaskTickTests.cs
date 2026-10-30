using PSPad.App.State;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TaskTickTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void AOneTimeTaskAlwaysTicks() =>
        Assert.True(TaskTick.Toggles(New("Buy milk"), Today));

    [Fact]
    public void ARepeatTicksOnADayItOccurs() =>
        Assert.True(TaskTick.Toggles(Repeating(RecurrenceRule.Daily(Today.AddDays(-1)).EveryNth(2)), Today.AddDays(1)));

    [Fact]
    public void ARepeatDoesNotTickOnADayItSkips() =>
        Assert.False(TaskTick.Toggles(Repeating(RecurrenceRule.Daily(Today.AddDays(-1)).EveryNth(2)), Today));

    [Fact]
    public void ARepeatDoesNotTickPastItsUntilDate()
    {
        var task = Repeating(RecurrenceRule.Daily(Today.AddDays(-7)));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today.AddDays(-1)), DateTimeOffset.UnixEpoch));

        Assert.False(TaskTick.Toggles(task, Today));
    }

    [Fact]
    public void ARepeatTickedOnADayUnticksIt()
    {
        var task = Repeating(RecurrenceRule.Daily(Today));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), DateTimeOffset.UnixEpoch));

        Assert.True(TaskTick.Toggles(task, Today));
    }

    static TodoTask Repeating(RecurrenceRule rule)
    {
        var task = New("Water plants");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask New(string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        return task;
    }
}
