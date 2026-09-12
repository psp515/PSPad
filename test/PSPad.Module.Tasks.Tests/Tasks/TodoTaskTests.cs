using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TodoTaskTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ATaskIsCreatedInAListWithNoDueDateAndNoPriority()
    {
        var task = Existing();

        Assert.Null(task.DueOn);
        Assert.Equal(Priority.None, task.Priority);
        Assert.False(task.Starred);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void ATaskWithoutAListIsRejected()
    {
        var command = new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.Empty, "Buy milk");

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(null, command, Now));
    }

    [Fact]
    public void SettingADueDateRecordsIt()
    {
        var task = Existing();
        var due = new DateOnly(2026, 9, 14);

        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), Now));

        Assert.Equal(due, task.DueOn);
    }

    [Fact]
    public void ClearingADueDateSetsItBackToNothing()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, new DateOnly(2026, 9, 14)), Now));

        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, null), Now));

        Assert.Null(task.DueOn);
    }

    [Fact]
    public void StarringIsImportanceOnlyAndCarriesNoDate()
    {
        var task = Existing();

        task.ApplyAll(TodoTask.Decide(task, new StarTask(Guid.NewGuid(), User, task.Id, true), Now));

        Assert.True(task.Starred);
        Assert.Null(task.DueOn);
    }

    [Fact]
    public void CompletingATaskStampsTheMoment()
    {
        var task = Existing();

        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        Assert.Equal(Now, task.CompletedAt);
    }

    [Fact]
    public void CompletingATaskTwiceProducesNoSecondEvent()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        Assert.Empty(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));
    }

    [Fact]
    public void ReopeningClearsTheCompletion()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        task.ApplyAll(TodoTask.Decide(task, new ReopenTask(Guid.NewGuid(), User, task.Id), Now));

        Assert.Null(task.CompletedAt);
    }

    internal static TodoTask Existing()
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(Guid.NewGuid(), User, Now, Guid.NewGuid(), "Buy milk"));
        return task;
    }
}
