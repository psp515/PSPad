using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public static class MongoBackfill
{
    public static async Task EnsureCreatedAtAsync(MongoContext context, CancellationToken ct)
    {
        var tasks = context.Collection<BsonDocument>("todotasks");
        var events = context.Collection<BsonDocument>("events");
        var sequence = new SequenceSource(context);

        var missing = await tasks
            .Find(Builders<BsonDocument>.Filter.Exists("createdAt", false))
            .ToListAsync(ct);

        foreach (var document in missing)
        {
            var created = await events
                .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", document["_id"]) &
                      Builders<BsonDocument>.Filter.Eq("type", "TaskCreated"))
                .SortBy(entry => entry["seq"])
                .FirstOrDefaultAsync(ct);

            if (created is null)
            {
                continue;
            }

            using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
            var seq = await sequence.NextAsync(session, ct);

            await tasks.UpdateOneAsync(
                session,
                Builders<BsonDocument>.Filter.Eq("_id", document["_id"]),
                Builders<BsonDocument>.Update.Set("createdAt", created["at"]).Set("seq", seq),
                cancellationToken: ct);
        }
    }
}
