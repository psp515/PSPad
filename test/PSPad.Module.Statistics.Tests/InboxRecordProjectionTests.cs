using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class InboxRecordProjectionTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid InboxId = Guid.NewGuid();

    sealed class FakeStore : IInboxRecordStore
    {
        public List<InboxRecord> Saved { get; } = [];

        public Task SaveAsync(InboxRecord record, CancellationToken ct)
        {
            Saved.RemoveAll(existing => existing.Id == record.Id);
            Saved.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<InboxRecord>> SinceAsync(
            Guid userId, DateTimeOffset from, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    static Task Handle(FakeStore store, long seq, DomainEvent @event) =>
        new InboxRecordProjection(store).HandleAsync(
            new DomainEventEnvelope(seq, @event), CancellationToken.None);

    [Fact]
    public async Task CapturingAnItemRecordsItAgainstTheEventsSeq()
    {
        var store = new FakeStore();
        var itemId = Guid.NewGuid();

        await Handle(store, 7, new InboxItemCaptured(InboxId, User, At, itemId, "Call the dentist", 0));

        var record = Assert.Single(store.Saved);
        Assert.Equal(7, record.Id);
        Assert.Equal(User, record.UserId);
        Assert.Equal(At, record.At);
        Assert.Equal(itemId, record.ItemId);
    }

    [Fact]
    public async Task OrganisingAnItemRecordsNothingBecauseOnlyCapturesAreCounted()
    {
        var store = new FakeStore();

        await Handle(
            store, 8,
            new InboxItemOrganised(InboxId, User, At, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task DiscardingAnItemRecordsNothingBecauseOnlyCapturesAreCounted()
    {
        var store = new FakeStore();

        await Handle(store, 9, new InboxItemDiscarded(InboxId, User, At, Guid.NewGuid()));

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task EmptyingAnItemLeavesItsCaptureStanding()
    {
        var store = new FakeStore();
        var itemId = Guid.NewGuid();

        await Handle(store, 3, new InboxItemCaptured(InboxId, User, At, itemId, "Ring the plumber", 0));
        await Handle(store, 4, new InboxItemDiscarded(InboxId, User, At, itemId));

        Assert.Equal(itemId, Assert.Single(store.Saved).ItemId);
    }

    [Fact]
    public async Task ReplayingTheSameEventUpsertsOverItsOwnRow()
    {
        var store = new FakeStore();
        var captured = new InboxItemCaptured(InboxId, User, At, Guid.NewGuid(), "Buy stamps", 0);

        await Handle(store, 4, captured);
        await Handle(store, 4, captured);

        Assert.Single(store.Saved);
    }

    [Fact]
    public async Task AnEventTheInboxDidNotRaiseIsIgnored()
    {
        var store = new FakeStore();

        await Handle(
            store, 5,
            new TaskCreated(Guid.NewGuid(), User, At, Guid.NewGuid(), "Fix the sink"));

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task CreatingAnInboxRecordsNothingBecauseNoItemWasCaptured()
    {
        var store = new FakeStore();

        await Handle(store, 6, new InboxCreated(InboxId, User, At));

        Assert.Empty(store.Saved);
    }
}
