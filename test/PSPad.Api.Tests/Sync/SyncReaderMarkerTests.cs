using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Sync;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SyncReaderMarkerTests(MongoFixture fixture)
{
    [Fact]
    public async Task MarkerCoversADocumentWhoseSeqWasBumpedWithoutAMatchingEvent()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), userId, taskId, Guid.NewGuid(), "Task"), at));
        await context.Collection<TodoTask>().InsertOneAsync(task, cancellationToken: ct);

        await context.Collection<StoredEvent>("events").InsertOneAsync(new StoredEvent
        {
            Seq = 1,
            UserId = userId,
            AggregateType = nameof(TodoTask),
            AggregateId = taskId,
            Type = nameof(TaskCreated),
            Payload = "{}",
            At = at
        }, cancellationToken: ct);

        const long backfilledSeq = 5_000_000;
        await context.Collection<BsonDocument>("todotasks").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", taskId),
            Builders<BsonDocument>.Update.Set("seq", backfilledSeq),
            cancellationToken: ct);

        var reader = new SyncReader(context);

        var first = await reader.ReadAsync(userId, 0, ct);
        Assert.True(
            first.Marker >= backfilledSeq,
            $"marker {first.Marker} should cover backfilled seq {backfilledSeq}");

        var second = await reader.ReadAsync(userId, first.Marker, ct);
        Assert.Empty(second.Documents["todotasks"]);
    }
}
