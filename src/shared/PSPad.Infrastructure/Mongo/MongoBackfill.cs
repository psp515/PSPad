using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public static class MongoBackfill
{
    public static async Task EnsureCreatedAtAsync(MongoContext context, CancellationToken ct)
    {
        await EnsureCreatedAtAsync(context, "todotasks", "TaskCreated", ct);
        await EnsureCreatedAtAsync(context, "referenceitems", "ReferenceItemCreated", ct);
        await EnsureCreatedAtAsync(context, "tasklists", "TaskListCreated", ct);
    }

    static async Task EnsureCreatedAtAsync(
        MongoContext context, string collection, string createdEvent, CancellationToken ct)
    {
        var documents = context.Collection<BsonDocument>(collection);
        var events = context.Collection<BsonDocument>("events");
        var sequence = new SequenceSource(context);

        var missing = await documents
            .Find(Builders<BsonDocument>.Filter.Exists("createdAt", false))
            .ToListAsync(ct);

        foreach (var document in missing)
        {
            var created = await events
                .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", document["_id"]) &
                      Builders<BsonDocument>.Filter.Eq("type", createdEvent))
                .SortBy(entry => entry["seq"])
                .FirstOrDefaultAsync(ct);

            if (created is null)
            {
                continue;
            }

            using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
            var seq = await sequence.NextAsync(session, ct);

            await documents.UpdateOneAsync(
                session,
                Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
                Builders<BsonDocument>.Update.Set("createdAt", created["at"]).Set("seq", seq),
                cancellationToken: ct);
        }
    }
}
