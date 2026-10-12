using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public static class MongoIndexes
{
    static readonly string[] AggregateCollections =
    [
        "areas", "tasklists", "todotasks", "goals", "inboxes", "users", "referenceitems", "areaviews", "listviews",
        "budgets", "moneypreferences"
    ];

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

        await context.Collection<BsonDocument>("tasklists").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("areaId")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("_members.userId").Ascending("seq"))
        ], ct);

        await EnsureUniqueInviteTokenAsync(context, ct);

        await context.Collection<BsonDocument>("referenceitems").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("listId")),
            cancellationToken: ct);

        foreach (var name in new[] { "todotasks", "referenceitems" })
        {
            await context.Collection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("listId").Ascending("seq")),
                cancellationToken: ct);
            await context.Collection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("previousListId").Ascending("seq")),
                cancellationToken: ct);
        }

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

        await EnsureStatisticsAsync(context, ct);

        await context.Collection<BsonDocument>("processed_commands").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("at"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) }),
            cancellationToken: ct);

        await context.Collection<BsonDocument>("list_snapshots").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("token"),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("expiresAt"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("listId"))
        ], ct);

        await context.Collection<BsonDocument>("snapshot_visits").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("expiresAt"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("visitedAt"))
        ], ct);
    }

    public const string InviteTokenIndex = "inviteToken_unique";

    static async Task EnsureUniqueInviteTokenAsync(MongoContext context, CancellationToken ct)
    {
        var lists = context.Collection<BsonDocument>("tasklists");
        var existing = await (await lists.Indexes.ListAsync(ct)).ToListAsync(ct);
        if (existing.Any(index => index["name"] == "inviteToken_1"))
        {
            await lists.Indexes.DropOneAsync("inviteToken_1", ct);
        }

        await lists.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("inviteToken"),
                new CreateIndexOptions<BsonDocument>
                {
                    Name = InviteTokenIndex,
                    Unique = true,
                    PartialFilterExpression = Builders<BsonDocument>.Filter.Type("inviteToken", BsonType.String)
                }),
            cancellationToken: ct);
    }

    public static async Task EnsureStatisticsAsync(MongoContext context, CancellationToken ct)
    {
        await context.Collection<BsonDocument>("statistics_records").Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("seq")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("kind").Ascending("at")),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("taskId").Ascending("kind"))
        ], ct);

        await context.Collection<BsonDocument>("statistics_inbox_records").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("at")),
            cancellationToken: ct);

        await context.Collection<BsonDocument>("statistics_labels").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("userId")),
            cancellationToken: ct);
    }
}
