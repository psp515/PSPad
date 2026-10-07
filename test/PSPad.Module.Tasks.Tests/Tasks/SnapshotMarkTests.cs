using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class SnapshotMarkTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid SnapshotId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly ListAccess MemberAccess = new(Owner, Member);

    [Fact]
    public void AMarkOnTheTaskIsRecorded()
    {
        var task = Existing();

        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));

        Assert.Equal(new SnapshotMark(SnapshotId, null, Now), Assert.Single(task.SnapshotMarks));
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void AMarkOnAStepNeverChecksIt()
    {
        var task = WithStep();
        var stepId = task.Steps[0].Id;

        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, stepId, SnapshotId, true), Now));

        Assert.Equal(stepId, Assert.Single(task.SnapshotMarks).StepId);
        Assert.False(Assert.Single(task.Steps).Checked);
    }

    [Fact]
    public void UnmarkingRemovesThatMarkOnly()
    {
        var task = WithStep();
        var stepId = task.Steps[0].Id;
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, stepId, SnapshotId, true), Now));

        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, false), Now));

        Assert.Equal(stepId, Assert.Single(task.SnapshotMarks).StepId);
    }

    [Fact]
    public void AnUnchangedMarkEmitsNothing()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));

        Assert.Empty(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));

        var otherTask = Existing();
        Assert.Empty(TodoTask.Decide(
            otherTask, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, otherTask.Id, null, SnapshotId, false), Now));
    }

    [Fact]
    public void AnUnknownStepIsRejected()
    {
        var task = Existing();

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, Guid.NewGuid(), SnapshotId, true), Now));
    }

    [Fact]
    public void ADeletedTaskIsRejected()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), Owner, task.Id), Now));

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));
    }

    [Fact]
    public void ClearingDismissesEveryMark()
    {
        var task = WithStep();
        var stepId = task.Steps[0].Id;
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, stepId, SnapshotId, true), Now));

        task.ApplyAll(TodoTask.Decide(task, new ClearTaskSnapshotMarks(Guid.NewGuid(), Owner, task.Id), Now));

        Assert.Empty(task.SnapshotMarks);
    }

    [Fact]
    public void ClearingWithNoMarksEmitsNothing()
    {
        var task = Existing();

        Assert.Empty(TodoTask.Decide(task, new ClearTaskSnapshotMarks(Guid.NewGuid(), Owner, task.Id), Now));
    }

    [Fact]
    public void AMemberClears()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));

        var cleared = Assert.Single(TodoTask.Decide(
            task, new ClearTaskSnapshotMarks(Guid.NewGuid(), Member, task.Id), Now, MemberAccess));

        Assert.Equal(Owner, cleared.UserId);
    }

    [Fact]
    public void MarksNeverPutATaskOnToday()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), Owner, task.Id, null, SnapshotId, true), Now));

        Assert.Empty(PSPad.Module.Tasks.Today.TodayRule.Select([task], DateOnly.FromDateTime(Now.Date)));
    }

    static TodoTask Existing()
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(Guid.NewGuid(), Owner, Now, Guid.NewGuid(), "Buy milk"));
        return task;
    }

    static TodoTask WithStep()
    {
        var task = Existing();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), Owner, task.Id, Guid.NewGuid(), "Buy"), Now));
        return task;
    }
}
