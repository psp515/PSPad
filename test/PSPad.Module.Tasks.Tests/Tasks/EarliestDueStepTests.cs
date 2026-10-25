using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class EarliestDueStepTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Day = new(2026, 9, 12);

    [Fact]
    public void NoDatedStepMeansNone()
    {
        var task = TodoTaskTests.Existing();
        AddStep(task, "pack", null);

        Assert.Null(task.EarliestDueStep);
    }

    [Fact]
    public void ALaterStepWithTheEarliestDateWins()
    {
        var task = TodoTaskTests.Existing();
        AddStep(task, "book van", null);
        AddStep(task, "hand keys", Day.AddDays(7));
        var boxes = AddStep(task, "order boxes", Day.AddDays(1));

        Assert.Equal(boxes, task.EarliestDueStep!.Id);
    }

    [Fact]
    public void ACheckedStepNeverCounts()
    {
        var task = TodoTaskTests.Existing();
        var early = AddStep(task, "call", Day);
        var late = AddStep(task, "visit", Day.AddDays(3));
        task.ApplyAll(TodoTask.Decide(task, new CheckStep(Guid.NewGuid(), User, task.Id, early, true), Now));

        Assert.Equal(late, task.EarliestDueStep!.Id);
    }

    [Fact]
    public void EveryStepCheckedMeansNone()
    {
        var task = TodoTaskTests.Existing();
        var only = AddStep(task, "call", Day);
        task.ApplyAll(TodoTask.Decide(task, new CheckStep(Guid.NewGuid(), User, task.Id, only, true), Now));

        Assert.Null(task.EarliestDueStep);
    }

    [Fact]
    public void ATieGoesToTheStepHigherInTheList()
    {
        var task = TodoTaskTests.Existing();
        var first = AddStep(task, "first", Day);
        AddStep(task, "second", Day);

        Assert.Equal(first, task.EarliestDueStep!.Id);
    }

    static Guid AddStep(TodoTask task, string name, DateOnly? due)
    {
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, name), Now));
        if (due is not null)
        {
            task.ApplyAll(TodoTask.Decide(task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, due), Now));
        }
        return stepId;
    }
}
