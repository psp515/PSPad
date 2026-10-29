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

    [Fact]
    public void ANewTaskQueryWithoutAListIsStillANewTask()
    {
        Assert.True(TaskQuery.IsNewTask("https://pspad.local/?task=new&due=2026-10-24"));
        Assert.Null(TaskQuery.NewTaskListFrom("https://pspad.local/?task=new&due=2026-10-24"));
    }

    [Fact]
    public void AnExistingTaskIsNotANewTask()
    {
        Assert.False(TaskQuery.IsNewTask($"https://pspad.local/?task={Guid.NewGuid()}"));
        Assert.False(TaskQuery.IsNewTask("https://pspad.local/"));
    }

    [Fact]
    public void AMalformedDueIsIgnored()
    {
        Assert.Null(TaskQuery.NewTaskDueFrom("https://pspad.local/?task=new&due=24.10.2026"));
        Assert.True(TaskQuery.IsNewTask("https://pspad.local/?task=new&due=24.10.2026"));
    }

    [Fact]
    public void ADueOutsideANewTaskQueryMeansNothing()
    {
        Assert.Null(TaskQuery.NewTaskDueFrom($"https://pspad.local/?task={Guid.NewGuid()}&due=2026-10-24"));
    }

    [Fact]
    public void ANewTaskQueryCarriesItsStartTime()
    {
        Assert.Equal(
            "https://pspad.local/?day=2026-10-26&task=new&due=2026-10-26&time=14:30",
            TaskQuery.ForNewTask("https://pspad.local/?day=2026-10-26", due: new DateOnly(2026, 10, 26), time: new TimeOnly(14, 30)));
        Assert.Equal(new TimeOnly(14, 30), TaskQuery.NewTaskTimeFrom("https://pspad.local/?task=new&due=2026-10-26&time=14:30"));
        Assert.Null(TaskQuery.NewTaskTimeFrom("https://pspad.local/?task=new&time=2pm"));
        Assert.Null(TaskQuery.NewTaskTimeFrom($"https://pspad.local/?task={Guid.NewGuid()}&time=14:30"));
    }

    [Fact]
    public void ForNewTaskWithADueAndNoListKeepsTheDayOnScreen()
    {
        Assert.Equal(
            "https://pspad.local/?day=2026-10-26&task=new&due=2026-10-26",
            TaskQuery.ForNewTask("https://pspad.local/?day=2026-10-26", due: new DateOnly(2026, 10, 26)));
    }
}
