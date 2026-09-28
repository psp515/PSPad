using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
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
    readonly FakeDocumentStore<ReferenceItem> _items = new();
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

    [Fact]
    public async Task AnItemIsCreatedInAReferenceList()
    {
        var list = SeedReferenceList();
        var itemId = Guid.NewGuid();

        var result = await new CreateReferenceItemHandler(_items, _lists, _work, _clock).HandleAsync(
            new CreateReferenceItem(Guid.NewGuid(), User, itemId, list.Id, "PLA Black", 0), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.Equal("PLA Black", ((ReferenceItem)_work.Staged.Single().Aggregate).Name);
    }

    [Fact]
    public async Task AnItemCannotBeCreatedInATaskList()
    {
        var list = SeedTaskList();

        var result = await new CreateReferenceItemHandler(_items, _lists, _work, _clock).HandleAsync(
            new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "PLA Black", 0), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task AnItemCannotBeCreatedInADeletedList()
    {
        var list = SeedReferenceList();
        list.ApplyAll(TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), User, list.Id), Now));

        var result = await new CreateReferenceItemHandler(_items, _lists, _work, _clock).HandleAsync(
            new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), list.Id, "PLA Black", 0), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task DeletingAReferenceListDeletesItsItemsInOneCommit()
    {
        var list = SeedReferenceList();
        var first = SeedItem(list.Id);
        var second = SeedItem(list.Id);
        var other = SeedReferenceList();
        var elsewhere = SeedItem(other.Id);

        var result = await new DeleteTaskListHandler(_lists, _tasks, _items, _work, _clock).HandleAsync(
            new DeleteTaskList(Guid.NewGuid(), User, list.Id), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.True(first.Deleted);
        Assert.True(second.Deleted);
        Assert.False(elsewhere.Deleted);
        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            _work.Events.OfType<ReferenceItemDeleted>().Select(deleted => deleted.AggregateId).Order());
    }

    [Fact]
    public async Task DeletingAnAreaDeletesItsListsItems()
    {
        var area = new Area();
        area.Apply(new AreaCreated(Guid.NewGuid(), User, Now, "Workshop", 0));
        var areas = new FakeDocumentStore<Area>();
        areas.Seed(area);

        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), User, Now, area.Id, "Filaments", 0, ListKind.Reference));
        _lists.Seed(list);
        var item = SeedItem(list.Id);

        var result = await new DeleteAreaHandler(areas, _lists, _tasks, _items, _work, _clock).HandleAsync(
            new DeleteArea(Guid.NewGuid(), User, area.Id), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.True(item.Deleted);
        Assert.Single(_work.Events.OfType<ReferenceItemDeleted>());
    }

    [Fact]
    public async Task AnItemCannotBeMovedIntoATaskList()
    {
        var origin = SeedReferenceList();
        var item = SeedItem(origin.Id);
        var taskList = SeedTaskList();

        var result = await new MoveReferenceItemToListHandler(_items, _lists, _work, _clock).HandleAsync(
            new MoveReferenceItemToList(Guid.NewGuid(), User, item.Id, taskList.Id), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task AnItemMovesBetweenReferenceLists()
    {
        var origin = SeedReferenceList();
        var item = SeedItem(origin.Id);
        var destination = SeedReferenceList();

        var result = await new MoveReferenceItemToListHandler(_items, _lists, _work, _clock).HandleAsync(
            new MoveReferenceItemToList(Guid.NewGuid(), User, item.Id, destination.Id), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(destination.Id, (await _items.LoadAsync(item.Id, CancellationToken.None))!.ListId);
    }

    [Fact]
    public async Task AnItemIsRenamedDescribedStarredAndDeleted()
    {
        var list = SeedReferenceList();
        var item = SeedItem(list.Id);

        Assert.True((await new RenameReferenceItemHandler(_items, _work, _clock).HandleAsync(
            new RenameReferenceItem(Guid.NewGuid(), User, item.Id, "PETG Grey"), CancellationToken.None)).Accepted);
        Assert.True((await new SetReferenceItemDescriptionHandler(_items, _work, _clock).HandleAsync(
            new SetReferenceItemDescription(Guid.NewGuid(), User, item.Id, "Dry 4h"), CancellationToken.None)).Accepted);
        Assert.True((await new StarReferenceItemHandler(_items, _work, _clock).HandleAsync(
            new StarReferenceItem(Guid.NewGuid(), User, item.Id, true), CancellationToken.None)).Accepted);
        Assert.True((await new DeleteReferenceItemHandler(_items, _work, _clock).HandleAsync(
            new DeleteReferenceItem(Guid.NewGuid(), User, item.Id), CancellationToken.None)).Accepted);

        var stored = await _items.LoadAsync(item.Id, CancellationToken.None);
        Assert.Equal("PETG Grey", stored!.Name);
        Assert.Equal("Dry 4h", stored.Description);
        Assert.True(stored.Starred);
        Assert.True(stored.Deleted);
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

    ReferenceItem SeedItem(Guid listId)
    {
        var item = new ReferenceItem();
        item.Apply(new ReferenceItemCreated(Guid.NewGuid(), User, Now, listId, "PLA Black", 0));
        _items.Seed(item);
        return item;
    }
}
