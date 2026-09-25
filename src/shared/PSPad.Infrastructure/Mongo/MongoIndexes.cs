using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public static class MongoIndexes
{
    static readonly string[] AggregateCollections =
        ["areas", "tasklists", "todotasks", "goals", "inboxes", "users"];

    public static async Task EnsureAsync(MongoContext context, CancellationToken ct)
    {
        foreach (var name in AggregateCollections)
        {
            await context.Collection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("seq")),
                cancellationToken: ct);
        }

        await context.Collection<BsonDocument>("todotasks").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("listId")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("dueOn"))
        ], ct);

        await context.Collection<BsonDocument>("tasklists").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("areaId")),
            cancellationToken: ct);

        await context.Collection<BsonDocument>("events").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("seq")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("at")),
            // Replay reads forward across every user by seq alone, which no userId-first index serves.
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("seq"))
        ], ct);

        await context.Collection<BsonDocument>("statistics_records").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("_id")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("kind").Ascending("at")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("taskId").Ascending("kind"))
        ], ct);

        await context.Collection<BsonDocument>("statistics_labels").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("userId")),
            cancellationToken: ct);

        await context.Collection<BsonDocument>("processed_commands").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("at"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) }),
            cancellationToken: ct);
    }
}
