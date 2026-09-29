using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TaskDescriptionTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    static TodoTask Task()
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Print a benchy"), Now));
        return task;
    }

    [Fact]
    public void ANewTaskHasNoDescription() => Assert.Equal("", Task().Description);

    [Fact]
    public void SettingADescriptionKeepsItsMarkdown()
    {
        var task = Task();

        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "## Settings\n- 0.2 mm"), Now));

        Assert.Equal("## Settings\n- 0.2 mm", task.Description);
    }

    [Fact]
    public void TrailingWhitespaceIsTrimmedButLeadingIndentIsKept()
    {
        var task = Task();

        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "    code\n\n  "), Now));

        Assert.Equal("    code", task.Description);
    }

    [Fact]
    public void AnUnchangedDescriptionEmitsNothing()
    {
        var task = Task();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "same"), Now));

        Assert.Empty(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "same  "), Now));
    }

    [Fact]
    public void AnEmptyDescriptionClearsIt()
    {
        var task = Task();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "x"), Now));

        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "   "), Now));

        Assert.Equal("", task.Description);
    }

    [Fact]
    public void ANullDescriptionFromTheWireClearsIt()
    {
        var task = Task();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "x"), Now));

        task.ApplyAll(TodoTask.Decide(task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, null!), Now));

        Assert.Equal("", task.Description);
    }
}
