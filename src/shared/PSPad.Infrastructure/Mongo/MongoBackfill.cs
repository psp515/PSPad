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

    // Goal links moved from TodoTask.GoalId onto Goal._taskIds (ADR-0065); a link to another user's goal is dropped.
    public static async Task EnsureGoalLinksAsync(MongoContext context, CancellationToken ct)
    {
        var tasks = context.Collection<BsonDocument>("todotasks");
        var goals = context.Collection<BsonDocument>("goals");
        var sequence = new SequenceSource(context);
        var withGoal = Builders<BsonDocument>.Filter.Exists("goalId");

        var linked = await tasks
            .Find(withGoal & Builders<BsonDocument>.Filter.Ne("goalId", BsonNull.Value))
            .ToListAsync(ct);

        foreach (var task in linked)
        {
            using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
            var seq = await sequence.NextAsync(session, ct);

            await goals.UpdateOneAsync(
                session,
                Builders<BsonDocument>.Filter.Eq("_id", task["goalId"]) &
                Builders<BsonDocument>.Filter.Eq("userId", task["userId"]) &
                Builders<BsonDocument>.Filter.Ne("_taskIds", task["_id"]),
                Builders<BsonDocument>.Update.AddToSet("_taskIds", task["_id"]).Set("seq", seq),
                cancellationToken: ct);
        }

        await tasks.UpdateManyAsync(withGoal, Builders<BsonDocument>.Update.Unset("goalId"), cancellationToken: ct);
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
