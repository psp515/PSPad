using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public sealed class SequenceSource(MongoContext context)
{
    public async Task<long> NextAsync(IClientSessionHandle session, CancellationToken ct)
    {
        var counters = context.Collection<BsonDocument>("counters");
        var updated = await counters.FindOneAndUpdateAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", "events"),
            Builders<BsonDocument>.Update.Inc("value", 1L),
            new FindOneAndUpdateOptions<BsonDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            ct);

        return updated["value"].ToInt64();
    }
}
