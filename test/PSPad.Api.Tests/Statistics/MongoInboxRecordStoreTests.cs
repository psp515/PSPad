using PSPad.Api.Statistics;
using PSPad.Module.Statistics;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoInboxRecordStoreTests(MongoFixture fixture)
{
    static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    MongoInboxRecordStore Store() => new(Persistence.TestContext.For(fixture));

    static InboxRecord Record(long id, Guid userId, InboxRecordKind kind, Guid itemId, DateTimeOffset at) =>
        new() { Id = id, UserId = userId, At = at, Kind = kind, ItemId = itemId };

    [Fact]
    public async Task TheWindowSplitsOnTheSameInstantWhicheverOffsetItCarries()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var before = Guid.NewGuid();
        var inside = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Captured, before, Noon.AddHours(-1)), ct);
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Captured, inside, Noon.AddHours(1)), ct);

        var from = Noon.ToOffset(TimeSpan.FromHours(2));
        var since = await store.SinceAsync(userId, from, ct);
        var held = await store.HeldItemIdsBeforeAsync(userId, from, ct);

        Assert.Equal([inside], since.Select(record => record.ItemId));
        Assert.Equal([before], held);
    }

    [Fact]
    public async Task AnItemEmptiedBeforeTheWindowIsNoLongerHeld()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var organised = Guid.NewGuid();
        var discarded = Guid.NewGuid();
        var waiting = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Captured, organised, Noon.AddHours(-3)), ct);
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Organised, organised, Noon.AddHours(-2)), ct);
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Captured, discarded, Noon.AddHours(-3)), ct);
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Discarded, discarded, Noon.AddHours(-2)), ct);
        await store.SaveAsync(Record(Seq(), userId, InboxRecordKind.Captured, waiting, Noon.AddHours(-3)), ct);

        var held = await store.HeldItemIdsBeforeAsync(userId, Noon, ct);

        Assert.Equal([waiting], held);
    }

    [Fact]
    public async Task AnotherUsersCapturesAreInvisible()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var store = Store();
        await store.SaveAsync(Record(Seq(), theirs, InboxRecordKind.Captured, Guid.NewGuid(), Noon.AddHours(-1)), ct);
        await store.SaveAsync(Record(Seq(), theirs, InboxRecordKind.Captured, Guid.NewGuid(), Noon.AddHours(1)), ct);

        Assert.Empty(await store.SinceAsync(mine, Noon, ct));
        Assert.Empty(await store.HeldItemIdsBeforeAsync(mine, Noon, ct));
    }

    [Fact]
    public async Task SavingTheSameSeqTwiceLeavesOneRecord()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var seq = Seq();
        var itemId = Guid.NewGuid();
        await store.SaveAsync(Record(seq, userId, InboxRecordKind.Captured, itemId, Noon), ct);
        await store.SaveAsync(Record(seq, userId, InboxRecordKind.Captured, itemId, Noon), ct);

        Assert.Single(await store.SinceAsync(userId, Noon.AddDays(-1), ct));
    }

    static long _seq = DateTimeOffset.UtcNow.Ticks;

    static long Seq() => Interlocked.Increment(ref _seq);
}
