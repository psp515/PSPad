using PSPad.Api.Statistics;
using PSPad.Module.Statistics;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoStatisticsStoreTests(MongoFixture fixture)
{
    static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    MongoStatisticsStore Store() => new(Persistence.TestContext.For(fixture));

    static StatisticsRecord Record(
        long id, Guid userId, RecordKind kind, Guid taskId, DateTimeOffset at, RecordRole role = RecordRole.Owner) =>
        new()
        {
            Id = StatisticsRecord.IdFor(id, userId),
            Seq = id,
            Role = role,
            UserId = userId,
            At = at,
            Kind = kind,
            TaskId = taskId,
            TaskName = "task"
        };

    [Fact]
    public async Task TheWindowSplitsOnTheSameInstantWhicheverOffsetItCarries()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var before = Guid.NewGuid();
        var inside = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, before, Noon.AddHours(-1)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, inside, Noon.AddHours(1)), ct);

        var from = Noon.ToOffset(TimeSpan.FromHours(2));
        var since = await store.SinceAsync(userId, from, ct);
        var open = await store.OpenTaskIdsBeforeAsync(userId, from, ct);

        Assert.Equal([inside], since.Select(record => record.TaskId));
        Assert.Equal([before], open);
    }

    [Fact]
    public async Task ARecordExactlyOnTheInstantFallsInsideTheWindow()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var taskId = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, taskId, Noon), ct);

        var from = Noon.ToOffset(TimeSpan.FromHours(-5));

        Assert.Single(await store.SinceAsync(userId, from, ct));
        Assert.Empty(await store.OpenTaskIdsBeforeAsync(userId, from, ct));
    }

    [Fact]
    public async Task ATaskFinishedBeforeTheWindowIsNoLongerOpen()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var finished = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var reopened = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, finished, Noon.AddHours(-3)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Completed, finished, Noon.AddHours(-2)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, deleted, Noon.AddHours(-3)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Deleted, deleted, Noon.AddHours(-2)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, reopened, Noon.AddHours(-3)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Completed, reopened, Noon.AddHours(-2)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Reopened, reopened, Noon.AddHours(-1)), ct);

        var open = await store.OpenTaskIdsBeforeAsync(userId, Noon, ct);

        Assert.Equal([reopened], open);
    }

    [Fact]
    public async Task AnActorsRecordNeverCountsAsOpen()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var ownTask = Guid.NewGuid();
        var sharedTask = Guid.NewGuid();
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, ownTask, Noon.AddHours(-1)), ct);
        await store.SaveAsync(Record(Seq(), userId, RecordKind.Created, sharedTask, Noon.AddHours(-1), RecordRole.Actor), ct);

        var open = await store.OpenTaskIdsBeforeAsync(userId, Noon, ct);

        Assert.Equal([ownTask], open);
    }

    [Fact]
    public async Task APageWalksBackwardsFromTheMarker()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var first = Seq();
        var second = Seq();
        await store.SaveAsync(Record(first, userId, RecordKind.Created, Guid.NewGuid(), Noon), ct);
        await store.SaveAsync(Record(second, userId, RecordKind.Created, Guid.NewGuid(), Noon), ct);

        var newest = await store.PageAsync(userId, null, 1, ct);
        var older = await store.PageAsync(userId, newest[0].Seq, 1, ct);

        Assert.Equal(second, newest[0].Seq);
        Assert.Equal(first, Assert.Single(older).Seq);
    }

    [Fact]
    public async Task SavingTheSameSeqTwiceLeavesOneRecord()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var seq = Seq();
        var taskId = Guid.NewGuid();
        await store.SaveAsync(Record(seq, userId, RecordKind.Created, taskId, Noon), ct);
        await store.SaveAsync(Record(seq, userId, RecordKind.Created, taskId, Noon), ct);

        Assert.Single(await store.PageAsync(userId, null, 50, ct));
    }

    [Fact]
    public async Task CompletionsAreCountedOnlyBelowTheGivenSeq()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var store = Store();
        var taskId = Guid.NewGuid();
        var earlier = Seq();
        var later = Seq();
        await store.SaveAsync(Record(earlier, userId, RecordKind.Completed, taskId, Noon), ct);
        await store.SaveAsync(Record(later, userId, RecordKind.Completed, taskId, Noon), ct);

        Assert.Equal(1, await store.CountCompletionsBeforeAsync(userId, taskId, later, ct));
        Assert.Equal(0, await store.CountCompletionsBeforeAsync(userId, taskId, earlier, ct));
    }

    static long _seq = DateTimeOffset.UtcNow.Ticks;

    static long Seq() => Interlocked.Increment(ref _seq);
}
