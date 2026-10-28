using PSPad.App.State;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TaskDoneTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void AnOpenOneTimeTaskIsNotDone() =>
        Assert.False(TaskDone.On(New("Buy milk"), Today));

    [Fact]
    public void ACompletedOneTimeTaskIsDone()
    {
        var task = New("Buy milk");
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), DateTimeOffset.UnixEpoch));

        Assert.True(TaskDone.On(task, Today));
    }

    [Fact]
    public void ARepeatTickedForTheDayIsDoneThatDayOnly()
    {
        var task = Recurring(New("Read a book"), Today.AddDays(-3));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), DateTimeOffset.UnixEpoch));

        Assert.True(TaskDone.On(task, Today));
        Assert.False(TaskDone.On(task, Today.AddDays(1)));
    }

    [Fact]
    public void ARepeatMissedLastWeekIsNotDone() =>
        Assert.False(TaskDone.On(Recurring(New("Read a book"), Today.AddDays(-7)), Today));

    [Fact]
    public void ARepeatPastItsUntilDateIsDone()
    {
        var task = Recurring(New("Read a book"), Today.AddDays(-7));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today.AddDays(-1)), DateTimeOffset.UnixEpoch));

        Assert.True(TaskDone.On(task, Today));
        Assert.False(TaskDone.On(task, Today.AddDays(-1)));
    }

    static TodoTask Recurring(TodoTask task, DateOnly from)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(from)), DateTimeOffset.UnixEpoch));
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
