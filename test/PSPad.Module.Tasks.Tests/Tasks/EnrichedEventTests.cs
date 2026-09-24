using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class EnrichedEventTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompletingATaskRecordsWhatItWasCalledAndWhereItLived()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var due = new DateOnly(2026, 9, 24);
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), userId, taskId, listId, "Fix the sink"), At));
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), userId, taskId, due), At));
        task.ApplyAll(TodoTask.Decide(task, new LinkTaskToGoal(Guid.NewGuid(), userId, taskId, goalId), At));

        var completed = Assert.IsType<TaskCompleted>(
            Assert.Single(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), userId, taskId), At)));

        Assert.Equal("Fix the sink", completed.Name);
        Assert.Equal(listId, completed.ListId);
        Assert.Equal(goalId, completed.GoalId);
        Assert.Equal(due, completed.DueOn);
    }

    [Fact]
    public void DeletingATaskRecordsTheNameItHadWhenItDied()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), userId, taskId, Guid.NewGuid(), "Old idea"), At));

        var deleted = Assert.IsType<TaskDeleted>(
            Assert.Single(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), userId, taskId), At)));

        Assert.Equal("Old idea", deleted.Name);
    }
}
