using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Tasks;
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

        var task = await LoadTask(context, taskId, ct);
        Assert.Equal(at, task.CreatedAt);
    }

    [Fact]
    public async Task ItLeavesAnExistingCreatedAtAlone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var stored = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        await InsertTask(context, userId, taskId, stored, ct);
        await InsertTaskCreatedEvent(
            context, userId, taskId, new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero), ct);

        var before = await LoadTask(context, taskId, ct);

        await MongoBackfill.EnsureCreatedAtAsync(context, ct);

        var task = await LoadTask(context, taskId, ct);
        Assert.Equal(stored, task.CreatedAt);
        Assert.Equal(before.Seq, task.Seq);
    }

    [Fact]
    public async Task ItBumpsSeqSoAnAlreadySyncedClientReceivesTheBackfilledValue()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

        await InsertTaskWithoutCreatedAt(context, userId, taskId, ct);
        await InsertTaskCreatedEvent(context, userId, taskId, at, ct);

        var since = await RaiseCounterFloorAsync(context, 1_000_000, ct);

        await MongoBackfill.EnsureCreatedAtAsync(context, ct);

        var task = await LoadTask(context, taskId, ct);
        Assert.True(task.Seq > since);

        var synced = await context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(document => document.UserId, userId) &
                  Builders<TodoTask>.Filter.Gt(document => document.Seq, since))
            .ToListAsync(ct);
        Assert.Contains(synced, document => document.Id == taskId);
    }

    static async Task<long> RaiseCounterFloorAsync(MongoContext context, long floor, CancellationToken ct)
    {
        var counters = context.Collection<BsonDocument>("counters");
        var updated = await counters.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "events"),
            Builders<BsonDocument>.Update.Max("value", floor),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);
        return updated["value"].ToInt64();
    }

    [Fact]
    public async Task ItLeavesAnOrphanTaskAloneAndDoesNotThrow()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await InsertTaskWithoutCreatedAt(context, userId, taskId, ct);

        await MongoBackfill.EnsureCreatedAtAsync(context, ct);

        var task = await LoadTask(context, taskId, ct);
        Assert.Null(task.CreatedAt);
    }

    static Task InsertTask(MongoContext context, Guid userId, Guid taskId, DateTimeOffset createdAt, CancellationToken ct) =>
        context.Collection<TodoTask>().InsertOneAsync(NewTask(userId, taskId, createdAt), cancellationToken: ct);

    static Task InsertTaskWithoutCreatedAt(MongoContext context, Guid userId, Guid taskId, CancellationToken ct)
    {
        var document = NewTask(userId, taskId, DateTimeOffset.UtcNow).ToBsonDocument();
        document.Remove("createdAt");
        return context.Collection<BsonDocument>("todotasks").InsertOneAsync(document, cancellationToken: ct);
    }

    static Task InsertTaskCreatedEvent(
        MongoContext context, Guid userId, Guid taskId, DateTimeOffset at, CancellationToken ct) =>
        context.Collection<StoredEvent>("events").InsertOneAsync(new StoredEvent
        {
            Seq = 1,
            UserId = userId,
            AggregateType = nameof(TodoTask),
            AggregateId = taskId,
            Type = nameof(TaskCreated),
            Payload = "{}",
            At = at
        }, cancellationToken: ct);

    static Task<TodoTask> LoadTask(MongoContext context, Guid taskId, CancellationToken ct) =>
        context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);

    static TodoTask NewTask(Guid userId, Guid taskId, DateTimeOffset createdAt)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), userId, taskId, Guid.NewGuid(), "Task"), createdAt));
        return task;
    }
}
