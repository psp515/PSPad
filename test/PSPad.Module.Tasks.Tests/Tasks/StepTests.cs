using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class StepTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StepsTakeConsecutivePositions()
    {
        var task = WithSteps("first", "second", "third");

        Assert.Equal([0, 1, 2], task.Steps.Select(step => step.Position));
        Assert.Equal(["first", "second", "third"], task.Steps.Select(step => step.Name));
    }

    [Fact]
    public void CheckingAStepMarksOnlyThatStep()
    {
        var task = WithSteps("first", "second");
        var target = task.Steps[0].Id;

        task.ApplyAll(TodoTask.Decide(task, new CheckStep(Guid.NewGuid(), User, task.Id, target, true), Now));

        Assert.True(task.Steps.Single(step => step.Id == target).Checked);
        Assert.False(task.Steps.Single(step => step.Id != target).Checked);
    }

    [Fact]
    public void TheNextUncheckedStepSkipsCheckedOnes()
    {
        var task = WithSteps("first", "second");
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, task.Steps[0].Id, true), Now));

        Assert.Equal("second", task.NextUncheckedStep!.Name);
    }

    [Fact]
    public void TheNextUncheckedStepIsNothingWhenEveryStepIsChecked()
    {
        var task = WithSteps("only");
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, task.Steps[0].Id, true), Now));

        Assert.Null(task.NextUncheckedStep);
    }

    [Fact]
    public void MovingAStepRewritesThePositionsDensely()
    {
        var task = WithSteps("first", "second", "third");
        var last = task.Steps[2].Id;

        task.ApplyAll(TodoTask.Decide(task, new MoveStep(Guid.NewGuid(), User, task.Id, last, 0), Now));

        Assert.Equal(["third", "first", "second"], task.Steps.Select(step => step.Name));
        Assert.Equal([0, 1, 2], task.Steps.Select(step => step.Position));
    }

    [Fact]
    public void ActingOnAStepThatIsNotThereIsRejected()
    {
        var task = WithSteps("only");

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), true), Now));
    }

    [Fact]
    public void RemovingAStepRewritesThePositionsDensely()
    {
        var task = WithSteps("first", "second", "third");
        var middle = task.Steps[1].Id;

        task.ApplyAll(TodoTask.Decide(task, new RemoveStep(Guid.NewGuid(), User, task.Id, middle), Now));

        Assert.Equal(["first", "third"], task.Steps.Select(step => step.Name));
        Assert.Equal([0, 1], task.Steps.Select(step => step.Position));
    }

    [Fact]
    public void AddingAStepWithARepeatedIdIsIgnored()
    {
        var task = WithSteps("only");
        var stepId = task.Steps[0].Id;

        var events = TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "again"), Now);
        task.ApplyAll(events);

        Assert.Empty(events);
        Assert.Single(task.Steps);
    }

    static TodoTask WithSteps(params string[] names)
    {
        var task = TodoTaskTests.Existing();
        foreach (var name in names)
        {
            task.ApplyAll(TodoTask.Decide(
                task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), name), Now));
        }

        return task;
    }
}
