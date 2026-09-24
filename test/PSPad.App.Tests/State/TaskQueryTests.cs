using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TaskQueryTests
{
    [Fact]
    public void AUrlWithNoQueryOpensNothing()
    {
        Assert.Null(TaskQuery.From("https://pspad.local/lists/" + Guid.NewGuid()));
    }

    [Fact]
    public void AUrlWithATaskOpensIt()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, TaskQuery.From($"https://pspad.local/?task={id}"));
    }

    [Fact]
    public void ANonGuidTaskOpensNothing()
    {
        Assert.Null(TaskQuery.From("https://pspad.local/?task=nonsense"));
    }

    [Fact]
    public void ClosingKeepsTheScreenAndDropsOnlyTheQuery()
    {
        var id = Guid.NewGuid();
        var list = Guid.NewGuid();

        Assert.Equal(
            $"https://pspad.local/lists/{list}",
            TaskQuery.Without($"https://pspad.local/lists/{list}?task={id}"));
    }

    [Fact]
    public void ANewTaskQueryNamesTheListItGoesInto()
    {
        var list = Guid.NewGuid();

        Assert.Equal(list, TaskQuery.NewTaskListFrom($"https://pspad.local/lists/{list}?task=new&list={list}"));
    }

    [Fact]
    public void ANewTaskQueryOpensNoExistingTask()
    {
        Assert.Null(TaskQuery.From($"https://pspad.local/?task=new&list={Guid.NewGuid()}"));
    }

    [Fact]
    public void AnExistingTaskQueryIsNotANewTask()
    {
        Assert.Null(TaskQuery.NewTaskListFrom($"https://pspad.local/?task={Guid.NewGuid()}"));
    }

    [Fact]
    public void ForNewTaskReplacesAnyOpenQueryOnTheSameScreen()
    {
        var list = Guid.NewGuid();

        Assert.Equal(
            $"https://pspad.local/lists/{list}?task=new&list={list}",
            TaskQuery.ForNewTask($"https://pspad.local/lists/{list}?task={Guid.NewGuid()}", list));
    }
}
