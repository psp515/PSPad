using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;
using InboxAggregate = PSPad.Module.Tasks.Inbox.Inbox;

namespace PSPad.Module.Tasks.Tests.Inbox;

[UnitTest]
public class InboxTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CapturingKeepsTheTextAndTheOrder()
    {
        var inbox = WithItems("call the dentist", "buy a gift");

        Assert.Equal(["call the dentist", "buy a gift"], inbox.Items.Select(item => item.Text));
    }

    [Fact]
    public void CapturingEmptyTextIsRejected()
    {
        var inbox = new InboxAggregate();
        inbox.Apply(new InboxCreated(Guid.NewGuid(), User, Now));

        Assert.Throws<DomainRejectedException>(() => InboxAggregate.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), "  "), Now));
    }

    [Fact]
    public void OrganisingRemovesTheItemFromTheInbox()
    {
        var inbox = WithItems("call the dentist");
        var itemId = inbox.Items[0].Id;

        inbox.ApplyAll(InboxAggregate.Decide(inbox, new OrganiseInboxItem(
            Guid.NewGuid(), User, inbox.Id, itemId, Guid.NewGuid(), Guid.NewGuid()), Now));

        Assert.Empty(inbox.Items);
    }

    [Fact]
    public async Task OrganisingCreatesTheTaskAndEmptiesTheItemInOneCommit()
    {
        var inbox = WithItems("call the dentist");
        var inboxStore = new FakeDocumentStore<InboxAggregate>();
        inboxStore.Seed(inbox);
        var taskStore = new FakeDocumentStore<TodoTask>();
        var work = new FakeUnitOfWork();
        var handler = new OrganiseInboxItemHandler(inboxStore, taskStore, work, new FixedClock(Now));

        var result = await handler.HandleAsync(
            new OrganiseInboxItem(Guid.NewGuid(), User, inbox.Id, inbox.Items[0].Id, Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(2, work.Staged.Count);
        Assert.Contains(work.Events, @event => @event is TaskCreated);
        Assert.Contains(work.Events, @event => @event is InboxItemOrganised);
    }

    [Fact]
    public void OrganisingRewritesThePositionsDensely()
    {
        var inbox = WithItems("call the dentist", "buy a gift", "return the parcel");
        var middle = inbox.Items[1].Id;

        inbox.ApplyAll(InboxAggregate.Decide(inbox, new OrganiseInboxItem(
            Guid.NewGuid(), User, inbox.Id, middle, Guid.NewGuid(), Guid.NewGuid()), Now));

        Assert.Equal(["call the dentist", "return the parcel"], inbox.Items.Select(item => item.Text));
        Assert.Equal([0, 1], inbox.Items.Select(item => item.Position));
    }

    [Fact]
    public void DiscardingRewritesThePositionsDensely()
    {
        var inbox = WithItems("call the dentist", "buy a gift", "return the parcel");
        var middle = inbox.Items[1].Id;

        inbox.ApplyAll(InboxAggregate.Decide(
            inbox, new DiscardInboxItem(Guid.NewGuid(), User, inbox.Id, middle), Now));

        Assert.Equal(["call the dentist", "return the parcel"], inbox.Items.Select(item => item.Text));
        Assert.Equal([0, 1], inbox.Items.Select(item => item.Position));
    }

    [Fact]
    public void RenamingAnItemKeepsItsPlaceAndCaptureTime()
    {
        var inbox = WithItems("call the dentist", "buy a gift");
        var item = inbox.Items[0];

        inbox.ApplyAll(InboxAggregate.Decide(inbox,
            new RenameInboxItem(Guid.NewGuid(), User, inbox.Id, item.Id, "  call the dentist on Monday "), Now.AddHours(1)));

        var renamed = inbox.Items[0];
        Assert.Equal("call the dentist on Monday", renamed.Text);
        Assert.Equal(item.Id, renamed.Id);
        Assert.Equal(item.CapturedAt, renamed.CapturedAt);
        Assert.Equal(item.Position, renamed.Position);
    }

    [Fact]
    public void RenamingToTheSameTextProducesNoEvent()
    {
        var inbox = WithItems("call the dentist");

        Assert.Empty(InboxAggregate.Decide(inbox,
            new RenameInboxItem(Guid.NewGuid(), User, inbox.Id, inbox.Items[0].Id, "call the dentist"), Now));
    }

    [Fact]
    public void RenamingToBlankTextOrAMissingItemIsRejected()
    {
        var inbox = WithItems("call the dentist");

        Assert.Throws<DomainRejectedException>(() => InboxAggregate.Decide(inbox,
            new RenameInboxItem(Guid.NewGuid(), User, inbox.Id, inbox.Items[0].Id, " "), Now));
        Assert.Throws<DomainRejectedException>(() => InboxAggregate.Decide(inbox,
            new RenameInboxItem(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), "x"), Now));
    }

    [Fact]
    public void CapturingWithARepeatedIdIsIgnored()
    {
        var inbox = WithItems("call the dentist");
        var itemId = inbox.Items[0].Id;

        var events = InboxAggregate.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, itemId, "again"), Now);
        inbox.ApplyAll(events);

        Assert.Empty(events);
        Assert.Single(inbox.Items);
    }

    static InboxAggregate WithItems(params string[] texts)
    {
        var inbox = new InboxAggregate();
        inbox.Apply(new InboxCreated(Guid.NewGuid(), User, Now));
        foreach (var text in texts)
        {
            inbox.ApplyAll(InboxAggregate.Decide(
                inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text), Now));
        }

        return inbox;
    }
}
