using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;
using InboxAggregate = PSPad.Module.Tasks.Inbox.Inbox;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class DeadContainerTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    readonly FakeDocumentStore<Area> _areas = new();
    readonly FakeDocumentStore<TaskList> _lists = new();
    readonly FakeDocumentStore<TodoTask> _tasks = new();
    readonly FakeDocumentStore<InboxAggregate> _inboxes = new();
    readonly FakeUnitOfWork _work = new();
    readonly FixedClock _clock = new(Now);

    [Fact]
    public async Task CreatingATaskInALiveListIsAccepted()
    {
        var list = SeedList(Guid.NewGuid());

        var result = await new CreateTaskHandler(_tasks, _lists, _work, _clock).HandleAsync(
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "buy milk"), CancellationToken.None);

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task CreatingATaskInADeletedListIsRejected()
    {
        var list = DeletedList();

        var result = await new CreateTaskHandler(_tasks, _lists, _work, _clock).HandleAsync(
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "buy milk"), CancellationToken.None);

        AssertRejectedUntouched(result, "list");
    }

    [Fact]
    public async Task CreatingATaskInAListThatNeverExistedIsRejected()
    {
        var result = await new CreateTaskHandler(_tasks, _lists, _work, _clock).HandleAsync(
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "buy milk"), CancellationToken.None);

        AssertRejectedUntouched(result, "list");
    }

    [Fact]
    public async Task MovingATaskIntoADeletedListIsRejected()
    {
        var task = SeedTask(SeedList(Guid.NewGuid()).Id);
        var gone = DeletedList();

        var result = await new MoveTaskToListHandler(_tasks, _lists, _work, _clock).HandleAsync(
            new MoveTaskToList(Guid.NewGuid(), User, task.Id, gone.Id), CancellationToken.None);

        AssertRejectedUntouched(result, "list");
    }

    [Fact]
    public async Task OrganisingAnInboxItemIntoADeletedListIsRejected()
    {
        var inbox = new InboxAggregate();
        inbox.Apply(new InboxCreated(Guid.NewGuid(), User, Now));
        inbox.ApplyAll(InboxAggregate.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), "call the dentist"), Now));
        _inboxes.Seed(inbox);
        var gone = DeletedList();

        var result = await new OrganiseInboxItemHandler(_inboxes, _tasks, _lists, _work, _clock).HandleAsync(
            new OrganiseInboxItem(Guid.NewGuid(), User, inbox.Id, inbox.Items[0].Id, gone.Id, Guid.NewGuid()),
            CancellationToken.None);

        AssertRejectedUntouched(result, "list");
        Assert.Single(inbox.Items);
    }

    [Fact]
    public async Task CreatingAListInADeletedAreaIsRejected()
    {
        var area = DeletedArea();

        var result = await new CreateTaskListHandler(_lists, _areas, _work, _clock).HandleAsync(
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), area.Id, "Errands", 0), CancellationToken.None);

        AssertRejectedUntouched(result, "area");
    }

    [Fact]
    public async Task CreatingAListInALiveAreaIsAccepted()
    {
        var area = SeedArea();

        var result = await new CreateTaskListHandler(_lists, _areas, _work, _clock).HandleAsync(
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), area.Id, "Errands", 0), CancellationToken.None);

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task MovingAListIntoADeletedAreaIsRejected()
    {
        var list = SeedList(SeedArea().Id);
        var gone = DeletedArea();

        var result = await new MoveTaskListToAreaHandler(_lists, _areas, _work, _clock).HandleAsync(
            new MoveTaskListToArea(Guid.NewGuid(), User, list.Id, gone.Id), CancellationToken.None);

        AssertRejectedUntouched(result, "area");
    }

    void AssertRejectedUntouched(CommandResult result, string container)
    {
        Assert.False(result.Accepted);
        Assert.Contains(container, result.Rejection, StringComparison.OrdinalIgnoreCase);
        Assert.False(_work.Committed);
    }

    Area SeedArea()
    {
        var area = new Area();
        area.Apply(new AreaCreated(Guid.NewGuid(), User, Now, "Home", 0));
        _areas.Seed(area);
        return area;
    }

    Area DeletedArea()
    {
        var area = SeedArea();
        area.ApplyAll(Area.Decide(area, new DeleteArea(Guid.NewGuid(), User, area.Id), Now));
        return area;
    }

    TaskList SeedList(Guid areaId)
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, areaId, "Errands", 0));
        _lists.Seed(list);
        return list;
    }

    TaskList DeletedList()
    {
        var list = SeedList(Guid.NewGuid());
        list.ApplyAll(TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), User, list.Id), Now));
        return list;
    }

    TodoTask SeedTask(Guid listId)
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(Guid.NewGuid(), User, Now, listId, "buy milk"));
        _tasks.Seed(task);
        return task;
    }
}
