using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class TaskListTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ALisIsCreatedInsideOneArea()
    {
        var areaId = Guid.NewGuid();
        var command = new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, "Errands", 0);

        var created = Assert.IsType<TaskListCreated>(
            Assert.Single(TaskList.Decide(null, command, Now)));

        Assert.Equal(areaId, created.AreaId);
        Assert.Equal("Errands", created.Name);
    }

    [Fact]
    public void ListsWithoutAnAreaAreRejected()
    {
        var command = new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.Empty, "Errands", 0);

        Assert.Throws<DomainRejectedException>(() => TaskList.Decide(null, command, Now));
    }

    [Fact]
    public void MovingToAnotherAreaEmitsAMove()
    {
        var list = Existing();
        var destination = Guid.NewGuid();

        var moved = Assert.IsType<TaskListMovedToArea>(Assert.Single(
            TaskList.Decide(list, new MoveTaskListToArea(Guid.NewGuid(), User, list.Id, destination), Now)));

        Assert.Equal(destination, moved.AreaId);
    }

    [Fact]
    public void MovingToTheAreaItIsAlreadyInProducesNoEvent()
    {
        var list = Existing();

        Assert.Empty(TaskList.Decide(
            list, new MoveTaskListToArea(Guid.NewGuid(), User, list.Id, list.AreaId), Now));
    }

    static TaskList Existing()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, Guid.NewGuid(), "Errands", 0));
        return list;
    }
}
