using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Sharing.Ports;

namespace PSPad.Api.Snapshots;

public sealed class MongoMarkBudget(MongoContext context) : IMarkBudget
{
    public const string CollectionName = "snapshot_mark_budgets";

    IMongoCollection<BsonDocument> Collection => context.Collection<BsonDocument>(CollectionName);

    public async Task<bool> TryTakeAsync(Guid listId, DateOnly day, int limit, CancellationToken ct)
    {
        var id = $"{listId}:{day:yyyy-MM-dd}";
        var underLimit = Builders<BsonDocument>.Filter.Eq("_id", id) & Builders<BsonDocument>.Filter.Lt("spent", limit);
        var take = Builders<BsonDocument>.Update
            .Inc("spent", 1)
            .SetOnInsert("expiresAt", day.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        try
        {
            var result = await Collection.UpdateOneAsync(underLimit, take, new UpdateOptions { IsUpsert = true }, ct);
            return result.ModifiedCount + (result.UpsertedId is null ? 0 : 1) > 0;
        }
        catch (MongoWriteException duplicate) when (duplicate.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // The day's row exists: either it is spent, or a concurrent first take inserted it a moment ago.
            var result = await Collection.UpdateOneAsync(underLimit, take, cancellationToken: ct);
            return result.ModifiedCount > 0;
        }
    }
}
