using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Sync;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SyncReaderSnapshotTests(MongoFixture fixture)
{
    [Fact]
    public async Task ReadAsyncDeliversAllRowsCoveredByTheReturnedMarker()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

        const long earlierCollisionSeq = 51;
        const long laterCollisionSeq = 52;

        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), userId, areaId, "Home", 0), at));
        await context.Collection<Area>().InsertOneAsync(area, cancellationToken: ct);
        await context.Collection<BsonDocument>("areas").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", areaId),
            Builders<BsonDocument>.Update.Set("seq", earlierCollisionSeq),
            cancellationToken: ct);

        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), userId, taskId, Guid.NewGuid(), "Task"), at));
        await context.Collection<TodoTask>().InsertOneAsync(task, cancellationToken: ct);
        await context.Collection<BsonDocument>("todotasks").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", taskId),
            Builders<BsonDocument>.Update.Set("seq", laterCollisionSeq),
            cancellationToken: ct);

        var reader = new SyncReader(context);

        var result = await reader.ReadAsync(userId, 0, ct);

        Assert.True(
            result.Marker >= laterCollisionSeq,
            $"marker {result.Marker} should cover seq {laterCollisionSeq}");
        Assert.Contains(result.Documents["areas"], row => row.GetProperty("id").GetGuid() == areaId);
        Assert.Contains(result.Documents["todotasks"], row => row.GetProperty("id").GetGuid() == taskId);

        var second = await reader.ReadAsync(userId, result.Marker, ct);
        Assert.Empty(second.Documents["areas"]);
        Assert.Empty(second.Documents["todotasks"]);
    }
}
