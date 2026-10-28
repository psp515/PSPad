using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class StepsDueByTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Day = new(2026, 9, 12);

    [Fact]
    public void UncheckedStepsDueOnOrBeforeTheDayCountInStepOrder()
    {
        var task = TodoTaskTests.Existing();
        var late = AddStep(task, "hand keys", Day.AddDays(4));
        var today = AddStep(task, "order boxes", Day);
        AddStep(task, "pack", null);
        var overdue = AddStep(task, "photos", Day.AddDays(-2));

        var steps = TodayRule.StepsDueBy(task, Day);

        Assert.Equal([today, overdue], steps.Select(step => step.Id));
        Assert.DoesNotContain(late, steps.Select(step => step.Id));
    }

    [Fact]
    public void ACheckedStepIsLeftOut()
    {
        var task = TodoTaskTests.Existing();
        var done = AddStep(task, "call", Day);
        task.ApplyAll(TodoTask.Decide(task, new CheckStep(Guid.NewGuid(), User, task.Id, done, true), Now));

        Assert.Empty(TodayRule.StepsDueBy(task, Day));
    }

    [Fact]
    public void ARepeatingTaskHasNone()
    {
        var task = TodoTaskTests.Existing();
        AddStep(task, "vacuum", Day);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Day.AddDays(-3))), Now));

        Assert.Empty(TodayRule.StepsDueBy(task, Day));
    }

    [Fact]
    public void ACompletedTaskHasNone()
    {
        var task = TodoTaskTests.Existing();
        AddStep(task, "call", Day);
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        Assert.Empty(TodayRule.StepsDueBy(task, Day));
    }

    static Guid AddStep(TodoTask task, string name, DateOnly? due)
    {
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, name), Now));
        if (due is { } day)
        {
            task.ApplyAll(TodoTask.Decide(task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, day), Now));
        }

        return stepId;
    }
}
