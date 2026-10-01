using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;
using InboxAggregate = PSPad.Module.Tasks.Inbox.Inbox;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class SharedTaskTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid ListId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    static readonly Guid TaskId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly ListAccess Access = new(Owner, Member);
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";

    [Fact]
    public void AMemberCreatesATaskOwnedByTheListOwner()
    {
        var created = Assert.IsType<TaskCreated>(Assert.Single(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), Member, TaskId, ListId, "Dune"), Now, Access)));
        Assert.Equal(Owner, created.UserId);
    }

    [Fact]
    public void AMemberRenamesCompletesAndTicksTheOwnersTask()
    {
        var task = OwnedTask();
        var renamed = Assert.Single(TodoTask.Decide(
            task, new RenameTask(Guid.NewGuid(), Member, task.Id, "Dune Messiah"), Now, Access));
        Assert.Equal(Owner, renamed.UserId);
        Assert.Single(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), Member, task.Id), Now, Access));
        Assert.Single(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), Member, task.Id, Guid.NewGuid(), "Buy"), Now, Access));
        Assert.Single(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), Member, task.Id), Now, Access));
    }

    [Fact]
    public void WithoutAccessAMemberIsStillAStranger() =>
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(OwnedTask(), new RenameTask(Guid.NewGuid(), Member, TaskId, "X"), Now));

    [Fact]
    public void AccessToAnotherOwnersListDoesNotReachThisTask() =>
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(
                OwnedTask(), new RenameTask(Guid.NewGuid(), Member, TaskId, "X"), Now, new ListAccess(Stranger, Member)));

    [Fact]
    public void OnlyTheOwnerLinksAGoal()
    {
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(OwnedTask(), new LinkTaskToGoal(Guid.NewGuid(), Member, TaskId, Guid.NewGuid()), Now, Access));
        Assert.Single(TodoTask.Decide(
            OwnedTask(), new LinkTaskToGoal(Guid.NewGuid(), Owner, TaskId, Guid.NewGuid()), Now, ListAccess.Owner(Owner)));
    }

    [Fact]
    public async Task TheHandlerLetsAMemberRenameThroughTheList()
    {
        var lists = new FakeDocumentStore<TaskList>();
        var tasks = new FakeDocumentStore<TodoTask>();
        lists.Seed(ListWithMember());
        tasks.Seed(OwnedTask());
        var work = new FakeUnitOfWork();

        var result = await new RenameTaskHandler(tasks, lists, work, new FixedClock(Now))
            .HandleAsync(new RenameTask(Guid.NewGuid(), Member, TaskId, "Dune Messiah"), CancellationToken.None);

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(Owner, Assert.Single(work.Events).UserId);
    }

    [Fact]
    public async Task TheHandlerRejectsAStranger()
    {
        var lists = new FakeDocumentStore<TaskList>();
        var tasks = new FakeDocumentStore<TodoTask>();
        lists.Seed(ListWithMember());
        tasks.Seed(OwnedTask());
        var work = new FakeUnitOfWork();

        var result = await new RenameTaskHandler(tasks, lists, work, new FixedClock(Now))
            .HandleAsync(new RenameTask(Guid.NewGuid(), Stranger, TaskId, "Dune Messiah"), CancellationToken.None);

        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task AMemberMovesATaskOnlyBetweenTheSameOwnersLists()
    {
        var lists = new FakeDocumentStore<TaskList>();
        var tasks = new FakeDocumentStore<TodoTask>();
        var membersOwnList = new TaskList();
        membersOwnList.Apply(new TaskListCreated(Guid.NewGuid(), Member, Now, Guid.NewGuid(), "Member's list"));
        lists.Seed(ListWithMember());
        lists.Seed(membersOwnList);
        tasks.Seed(OwnedTask());
        var work = new FakeUnitOfWork();

        var result = await new MoveTaskToListHandler(tasks, lists, work, new FixedClock(Now))
            .HandleAsync(new MoveTaskToList(Guid.NewGuid(), Member, TaskId, membersOwnList.Id), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("A task can only move between lists of the same owner.", result.Rejection);
    }

    [Fact]
    public async Task OrganisingIntoASharedListCreatesTheOwnersTask()
    {
        var inboxes = new FakeDocumentStore<InboxAggregate>();
        var tasks = new FakeDocumentStore<TodoTask>();
        var lists = new FakeDocumentStore<TaskList>();
        var inbox = new InboxAggregate();
        inbox.Apply(new InboxCreated(Guid.NewGuid(), Member, Now));
        inbox.ApplyAll(InboxAggregate.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), Member, inbox.Id, Guid.NewGuid(), "Read Dune"), Now));
        inboxes.Seed(inbox);
        lists.Seed(ListWithMember());
        var work = new FakeUnitOfWork();
        var handler = new OrganiseInboxItemHandler(inboxes, tasks, lists, work, new FixedClock(Now));

        var result = await handler.HandleAsync(
            new OrganiseInboxItem(Guid.NewGuid(), Member, inbox.Id, inbox.Items[0].Id, ListId, TaskId),
            CancellationToken.None);

        Assert.True(result.Accepted, result.Rejection);
        var stagedTask = Assert.Single(work.Staged, entry => entry.Aggregate is TodoTask);
        Assert.Equal(Owner, stagedTask.Aggregate.UserId);
        var stagedInbox = Assert.Single(work.Staged, entry => entry.Aggregate is InboxAggregate);
        Assert.Equal(Member, stagedInbox.Aggregate.UserId);
    }

    static TodoTask OwnedTask()
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), Owner, TaskId, ListId, "Dune"), Now));
        return task;
    }

    static TaskList ListWithMember()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, Guid.NewGuid(), "Errands"));
        list.Apply(new TaskListShared(list.Id, Owner, Now, Token, "Łukasz"));
        list.Apply(new TaskListJoined(list.Id, Owner, Now, Member, "Anna"));
        return list;
    }
}
