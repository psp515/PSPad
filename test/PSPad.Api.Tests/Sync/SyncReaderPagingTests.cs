using System.Text.Json;
using MongoDB.Driver;
using PSPad.Api.Sync;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SyncReaderPagingTests(MongoFixture fixture)
{
    static readonly DateTimeOffset At = new(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PagesCoverEveryDocumentOnceAcrossCollections()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var expected = new HashSet<Guid>();
        var seq = 0L;

        for (var index = 0; index < 7; index++)
        {
            expected.Add(await InsertTaskAsync(context, userId, ++seq, "Task", ct));
            expected.Add(await InsertAreaAsync(context, userId, ++seq, ct));
        }

        var pages = await ReadAllAsync(new SyncReader(context), userId, 0, 3, ct);

        var seen = pages.SelectMany(page => Ids(page, "todotasks").Concat(Ids(page, "areas"))).ToArray();
        Assert.True(pages.Count > 1);
        Assert.Equal(seen.Length, seen.Distinct().Count());
        Assert.Equal(expected, seen.ToHashSet());
        Assert.False(pages[^1].HasMore);
        Assert.All(pages, page => Assert.True(page.Documents["todotasks"].Length <= 3));
    }

    [Fact]
    public async Task AFullPageEndsAtItsHighestSeqSoTheNextPageStartsRightAfter()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var seq = 0L;

        for (var index = 0; index < 5; index++)
        {
            await InsertTaskAsync(context, userId, ++seq, "Task", ct);
        }

        var first = await new SyncReader(context).ReadAsync(userId, 0, [], ct, limit: 2);

        Assert.True(first.HasMore);
        Assert.Equal(2, first.Documents["todotasks"].Length);
        Assert.Equal(first.Documents["todotasks"].Max(row => row.GetProperty("seq").GetInt64()), first.Marker);
    }

    [Fact]
    public async Task ADocumentChangedBetweenPagesArrivesOnceInItsNewestVersion()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var seq = 0L;
        var reader = new SyncReader(context);

        var early = await InsertTaskAsync(context, userId, ++seq, "Before", ct);
        var late = await InsertTaskAsync(context, userId, ++seq, "Late", ct);
        await InsertTaskAsync(context, userId, ++seq, "Later", ct);

        var first = await reader.ReadAsync(userId, 0, [], ct, limit: 1);
        await context.Collection<TodoTask>().UpdateOneAsync(
            Builders<TodoTask>.Filter.Eq(task => task.Id, late),
            Builders<TodoTask>.Update.Set(task => task.Seq, seq + 10).Set("name", "After"),
            cancellationToken: ct);
        var rest = await ReadAllAsync(reader, userId, first.Marker, 1, ct);

        var lateRows = rest.SelectMany(page => page.Documents["todotasks"])
            .Where(row => row.GetProperty("id").GetGuid() == late)
            .ToArray();
        Assert.Equal([early], Ids(first, "todotasks"));
        Assert.Equal("After", Assert.Single(lateRows).GetProperty("name").GetString());
    }

    [Fact]
    public async Task SyncNoLongerShipsTheEventLog()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = await InsertTaskAsync(context, userId, 1, "Task", ct);
        await context.Collection<StoredEvent>("events").InsertOneAsync(new StoredEvent
        {
            Seq = 1,
            UserId = userId,
            AggregateType = nameof(TodoTask),
            AggregateId = taskId,
            Type = nameof(TaskCreated),
            Payload = "{}",
            At = At
        }, cancellationToken: ct);

        var page = await new SyncReader(context).ReadAsync(userId, 0, [], ct);

        var json = JsonSerializer.SerializeToElement(page, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(json.TryGetProperty("events", out _));
    }

    static async Task<List<SyncResponse>> ReadAllAsync(
        SyncReader reader, Guid userId, long since, int limit, CancellationToken ct)
    {
        var pages = new List<SyncResponse>();
        SyncResponse page;

        do
        {
            page = await reader.ReadAsync(userId, since, [], ct, limit);
            pages.Add(page);
            Assert.True(page.Marker >= since);
            since = page.Marker;
        }
        while (page.HasMore && pages.Count < 50);

        return pages;
    }

    static IEnumerable<Guid> Ids(SyncResponse page, string collection) =>
        page.Documents[collection].Select(row => row.GetProperty("id").GetGuid());

    static async Task<Guid> InsertTaskAsync(
        MongoContext context, Guid userId, long seq, string name, CancellationToken ct)
    {
        var taskId = Guid.NewGuid();
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), userId, taskId, Guid.NewGuid(), name), At));
        task.Seq = seq;
        await context.Collection<TodoTask>().InsertOneAsync(task, cancellationToken: ct);
        return taskId;
    }

    static async Task<Guid> InsertAreaAsync(MongoContext context, Guid userId, long seq, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), userId, areaId, "Area", 0), At));
        area.Seq = seq;
        await context.Collection<Area>().InsertOneAsync(area, cancellationToken: ct);
        return areaId;
    }
}
