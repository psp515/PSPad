using MongoDB.Bson;
using MongoDB.Driver;

namespace PSPad.Infrastructure.Mongo;

public sealed class DatasetIdentity(MongoContext context)
{
    const string Key = "dataset";
    const int DuplicateKey = 11000;

    public async Task<Guid> CurrentAsync(CancellationToken ct)
    {
        try
        {
            return await ReadOrCreateAsync(ct);
        }
        catch (MongoCommandException exception) when (exception.Code == DuplicateKey)
        {
            return await ReadOrCreateAsync(ct);
        }
    }

    async Task<Guid> ReadOrCreateAsync(CancellationToken ct)
    {
        var stored = await context.Collection<BsonDocument>("meta").FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Key),
            Builders<BsonDocument>.Update.SetOnInsert("value", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard)),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);

        return stored["value"].AsGuid;
    }
}
