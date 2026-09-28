using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;
using InboxAggregate = PSPad.Module.Tasks.Inbox.Inbox;

namespace PSPad.Module.Tasks.Tests.References;

[UnitTest]
public class ReferenceHandlerTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    readonly FakeDocumentStore<TaskList> _lists = new();
    readonly FakeDocumentStore<TodoTask> _tasks = new();
    readonly FakeDocumentStore<InboxAggregate> _inboxes = new();
    readonly FakeUnitOfWork _work = new();
    readonly FixedClock _clock = new(Now);

    [Fact]
    public async Task ATaskCannotBeCreatedInAReferenceList()
    {
        var list = SeedReferenceList();

        var result = await new CreateTaskHandler(_tasks, _lists, _work, _clock).HandleAsync(
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "buy milk"), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task ATaskCannotBeMovedIntoAReferenceList()
    {
        var origin = SeedTaskList();
        var task = SeedTask(origin.Id);
        var referenceList = SeedReferenceList();

        var result = await new MoveTaskToListHandler(_tasks, _lists, _work, _clock).HandleAsync(
            new MoveTaskToList(Guid.NewGuid(), User, task.Id, referenceList.Id), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task AnInboxItemCannotBeOrganisedIntoAReferenceList()
    {
        var inbox = new InboxAggregate();
        inbox.Apply(new InboxCreated(Guid.NewGuid(), User, Now));
        inbox.ApplyAll(InboxAggregate.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), "call the dentist"), Now));
        _inboxes.Seed(inbox);
        var referenceList = SeedReferenceList();

        var result = await new OrganiseInboxItemHandler(_inboxes, _tasks, _lists, _work, _clock).HandleAsync(
            new OrganiseInboxItem(Guid.NewGuid(), User, inbox.Id, inbox.Items[0].Id, referenceList.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
        Assert.Single(inbox.Items);
    }

    TaskList SeedTaskList()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, Guid.NewGuid(), "Errands", 0));
        _lists.Seed(list);
        return list;
    }

    TaskList SeedReferenceList()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, Guid.NewGuid(), "Filaments", 0, ListKind.Reference));
        _lists.Seed(list);
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
