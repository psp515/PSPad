using PSPad.Abstractions;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class SidebarCountsTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public async Task ItCountsWhatIsOnTodayAndWhatIsInTheInbox()
    {
        var replica = new InMemoryReplica();
        await replica.SaveAsync(DueTask("Buy milk", Today));
        await replica.SaveAsync(DueTask("Later", Today.AddDays(3)));
        await replica.SaveAsync(InboxWith("A thought", "Another"));

        var counts = Counts(replica);
        await counts.RefreshAsync();

        Assert.Equal(1, counts.Today);
        Assert.Equal(2, counts.Inbox);
    }

    [Fact]
    public async Task RefreshingRaisesChanged()
    {
        var counts = Counts(new InMemoryReplica());
        var raised = 0;
        counts.Changed += () => raised++;

        await counts.RefreshAsync();

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task AUserWithNoInboxHasAZeroCount()
    {
        var counts = Counts(new InMemoryReplica());

        await counts.RefreshAsync();

        Assert.Equal(0, counts.Inbox);
    }

    static SidebarCounts Counts(InMemoryReplica replica) =>
        new(new ReplicaDocumentStore<TodoTask>(replica),
            new ReplicaDocumentStore<Inbox>(replica),
            new AppState { UserId = User, Today = Today });

    static TodoTask DueTask(string name, DateOnly due)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), DateTimeOffset.UnixEpoch));
        return task;
    }

    static Inbox InboxWith(params string[] texts)
    {
        var inbox = new Inbox();
        var inboxId = Guid.NewGuid();
        inbox.ApplyAll(Inbox.Decide(
            null, new CreateInbox(Guid.NewGuid(), User, inboxId), DateTimeOffset.UnixEpoch));

        foreach (var text in texts)
        {
            inbox.ApplyAll(Inbox.Decide(
                inbox,
                new CaptureToInbox(Guid.NewGuid(), User, inboxId, Guid.NewGuid(), text),
                DateTimeOffset.UnixEpoch));
        }

        return inbox;
    }
}
