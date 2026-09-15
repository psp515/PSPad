using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoBackfillTests(MongoFixture fixture)
{
    [Fact]
    public async Task ItFillsCreatedAtFromTheEarliestTaskCreatedEvent()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

        await InsertTaskWithoutCreatedAt(context, userId, taskId, ct);
        await InsertTaskCreatedEvent(context, userId, taskId, at, ct);

        await MongoBackfill.EnsureCreatedAtAsync(context, ct);

        var document = await LoadTask(context, taskId, ct);
        Assert.Equal(at.UtcDateTime, document["createdAt"].ToUniversalTime());
    }

    [Fact]
    public async Task ItLeavesAnExistingCreatedAtAlone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var stored = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        await InsertTaskWithCreatedAt(context, userId, taskId, stored, ct);
        await InsertTaskCreatedEvent(
            context, userId, taskId, new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero), ct);

        await MongoBackfill.EnsureCreatedAtAsync(context, ct);

        var document = await LoadTask(context, taskId, ct);
        Assert.Equal(stored.UtcDateTime, document["createdAt"].ToUniversalTime());
    }

    static Task InsertTaskWithoutCreatedAt(MongoContext context, Guid userId, Guid taskId, CancellationToken ct) =>
        context.Collection<BsonDocument>("todotasks").InsertOneAsync(TaskDocument(userId, taskId), cancellationToken: ct);

    static Task InsertTaskWithCreatedAt(
        MongoContext context, Guid userId, Guid taskId, DateTimeOffset createdAt, CancellationToken ct)
    {
        var document = TaskDocument(userId, taskId);
        document["createdAt"] = createdAt.UtcDateTime;
        return context.Collection<BsonDocument>("todotasks").InsertOneAsync(document, cancellationToken: ct);
    }

    static Task InsertTaskCreatedEvent(
        MongoContext context, Guid userId, Guid taskId, DateTimeOffset at, CancellationToken ct) =>
        context.Collection<BsonDocument>("events").InsertOneAsync(new BsonDocument
        {
            { "seq", 1L },
            { "userId", new BsonBinaryData(userId, GuidRepresentation.Standard) },
            { "aggregateType", "TodoTask" },
            { "aggregateId", new BsonBinaryData(taskId, GuidRepresentation.Standard) },
            { "type", "TaskCreated" },
            { "payload", "{}" },
            { "at", at.UtcDateTime }
        }, cancellationToken: ct);

    static Task<BsonDocument> LoadTask(MongoContext context, Guid taskId, CancellationToken ct) =>
        context.Collection<BsonDocument>("todotasks")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(taskId, GuidRepresentation.Standard)))
            .SingleAsync(ct);

    static BsonDocument TaskDocument(Guid userId, Guid taskId) => new()
    {
        { "_id", new BsonBinaryData(taskId, GuidRepresentation.Standard) },
        { "userId", new BsonBinaryData(userId, GuidRepresentation.Standard) },
        { "version", 1 },
        { "deleted", false },
        { "listId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
        { "name", "Task" }
    };
}
