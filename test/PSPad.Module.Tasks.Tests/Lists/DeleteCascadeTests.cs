using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class DeleteCascadeTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    readonly FakeDocumentStore<Area> _areas = new();
    readonly FakeDocumentStore<TaskList> _lists = new();
    readonly FakeDocumentStore<TodoTask> _tasks = new();
    readonly FakeDocumentStore<ReferenceItem> _items = new();
    readonly FakeUnitOfWork _work = new();

    [Fact]
    public async Task DeletingAListDeletesItsTasksInOneCommit()
    {
        var list = SeedList(Guid.NewGuid());
        var first = SeedTask(list.Id, "call the dentist");
        var second = SeedTask(list.Id, "buy a gift");

        var result = await DeleteList(list.Id);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.True(list.Deleted);
        Assert.True(first.Deleted);
        Assert.True(second.Deleted);
        Assert.Single(_work.Events.OfType<TaskListDeleted>());
        Assert.Equal(
            ["buy a gift", "call the dentist"],
            _work.Events.OfType<TaskDeleted>().Select(deleted => deleted.Name).Order());
    }

    [Fact]
    public async Task DeletingAListLeavesTasksInOtherListsAlone()
    {
        var list = SeedList(Guid.NewGuid());
        var other = SeedList(Guid.NewGuid());
        var elsewhere = SeedTask(other.Id, "water the plants");

        await DeleteList(list.Id);

        Assert.False(elsewhere.Deleted);
        Assert.Empty(_work.Events.OfType<TaskDeleted>());
    }

    [Fact]
    public async Task DeletingAListDoesNotDeleteAnAlreadyDeletedTaskAgain()
    {
        var list = SeedList(Guid.NewGuid());
        var gone = SeedTask(list.Id, "old");
        gone.ApplyAll(TodoTask.Decide(gone, new DeleteTask(Guid.NewGuid(), User, gone.Id), Now));

        await DeleteList(list.Id);

        Assert.Empty(_work.Events.OfType<TaskDeleted>());
    }

    [Fact]
    public async Task DeletingAnAlreadyDeletedListCascadesNothing()
    {
        var list = SeedList(Guid.NewGuid());
        list.ApplyAll(TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), User, list.Id), Now));
        var task = SeedTask(list.Id, "left behind before the cascade existed");

        var result = await DeleteList(list.Id);

        Assert.False(result.Accepted);
        Assert.False(task.Deleted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task DeletingAnAreaDeletesItsListsAndTheirTasksInOneCommit()
    {
        var area = SeedArea();
        var errands = SeedList(area.Id);
        var chores = SeedList(area.Id);
        var tasks = new[]
        {
            SeedTask(errands.Id, "post office"),
            SeedTask(errands.Id, "pharmacy"),
            SeedTask(chores.Id, "vacuum")
        };

        var result = await DeleteArea(area.Id);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.True(area.Deleted);
        Assert.True(errands.Deleted);
        Assert.True(chores.Deleted);
        Assert.All(tasks, task => Assert.True(task.Deleted));
        Assert.Single(_work.Events.OfType<AreaDeleted>());
        Assert.Equal(2, _work.Events.OfType<TaskListDeleted>().Count());
        Assert.Equal(3, _work.Events.OfType<TaskDeleted>().Count());
    }

    [Fact]
    public async Task DeletingAnAreaLeavesOtherAreasListsAndTasksAlone()
    {
        var area = SeedArea();
        var otherList = SeedList(Guid.NewGuid());
        var otherTask = SeedTask(otherList.Id, "keep me");

        await DeleteArea(area.Id);

        Assert.False(otherList.Deleted);
        Assert.False(otherTask.Deleted);
        Assert.Empty(_work.Events.OfType<TaskListDeleted>());
    }

    [Fact]
    public async Task DeletingAnAreaSkipsListsAlreadyDeleted()
    {
        var area = SeedArea();
        var gone = SeedList(area.Id);
        gone.ApplyAll(TaskList.Decide(gone, new DeleteTaskList(Guid.NewGuid(), User, gone.Id), Now));

        var result = await DeleteArea(area.Id);

        Assert.True(result.Accepted);
        Assert.Empty(_work.Events.OfType<TaskListDeleted>());
    }

    [Fact]
    public async Task DeletingSomebodyElsesAreaIsRejectedAndTouchesNothing()
    {
        var area = SeedArea();
        var list = SeedList(area.Id);

        var result = await new DeleteAreaHandler(_areas, _lists, _tasks, _items, _work, new FixedClock(Now))
            .HandleAsync(new DeleteArea(Guid.NewGuid(), Guid.NewGuid(), area.Id), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(list.Deleted);
        Assert.False(_work.Committed);
    }

    Task<CommandResult> DeleteList(Guid listId) =>
        new DeleteTaskListHandler(_lists, _tasks, _items, _work, new FixedClock(Now))
            .HandleAsync(new DeleteTaskList(Guid.NewGuid(), User, listId), CancellationToken.None);

    Task<CommandResult> DeleteArea(Guid areaId) =>
        new DeleteAreaHandler(_areas, _lists, _tasks, _items, _work, new FixedClock(Now))
            .HandleAsync(new DeleteArea(Guid.NewGuid(), User, areaId), CancellationToken.None);

    Area SeedArea()
    {
        var area = new Area();
        area.Apply(new AreaCreated(Guid.NewGuid(), User, Now, "Home", 0));
        _areas.Seed(area);
        return area;
    }

    TaskList SeedList(Guid areaId)
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, areaId, "Errands"));
        _lists.Seed(list);
        return list;
    }

    TodoTask SeedTask(Guid listId, string name)
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(Guid.NewGuid(), User, Now, listId, name));
        _tasks.Seed(task);
        return task;
    }
}
