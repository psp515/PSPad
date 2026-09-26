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

    static InboxRecord Record(long id, Guid userId, Guid itemId, DateTimeOffset at) =>
        new() { Id = id, UserId = userId, At = at, ItemId = itemId };

    [Fact]
    public async Task TheWindowSplitsOnTheSameInstantWhicheverOffsetItCarries()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var before = Guid.NewGuid();
        var inside = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, before, Noon.AddHours(-1)), ct);
        await store.SaveAsync(Record(Seq(), userId, inside, Noon.AddHours(1)), ct);

        var since = await store.SinceAsync(userId, Noon.ToOffset(TimeSpan.FromHours(2)), ct);

        Assert.Equal([inside], since.Select(record => record.ItemId));
    }

    [Fact]
    public async Task AnotherUsersCapturesAreInvisible()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var store = Store();
        await store.SaveAsync(Record(Seq(), theirs, Guid.NewGuid(), Noon.AddHours(-1)), ct);
        await store.SaveAsync(Record(Seq(), theirs, Guid.NewGuid(), Noon.AddHours(1)), ct);

        Assert.Empty(await store.SinceAsync(mine, Noon, ct));
    }

    [Fact]
    public async Task SavingTheSameSeqTwiceLeavesOneRecord()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var seq = Seq();
        var itemId = Guid.NewGuid();
        await store.SaveAsync(Record(seq, userId, itemId, Noon), ct);
        await store.SaveAsync(Record(seq, userId, itemId, Noon), ct);

        Assert.Single(await store.SinceAsync(userId, Noon.AddDays(-1), ct));
    }

    static long _seq = DateTimeOffset.UtcNow.Ticks;

    static long Seq() => Interlocked.Increment(ref _seq);
}
