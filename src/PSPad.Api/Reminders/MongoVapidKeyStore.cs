using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Reminders;

namespace PSPad.Api.Reminders;

public sealed class MongoVapidKeyStore(MongoContext context) : IVapidKeyStore
{
    public const string CollectionName = "vapidkeys";
    const string FixedId = "vapid";

    static MongoVapidKeyStore()
    {
        BsonClassMap.RegisterClassMap<VapidKeysDocument>(map =>
        {
            map.AutoMap();
            map.MapIdMember(document => document.Id);
            map.MapMember(document => document.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        });
    }

    IMongoCollection<VapidKeysDocument> Collection => context.Collection<VapidKeysDocument>(CollectionName);

    public async Task<VapidKeys> GetOrCreateAsync(Func<VapidKeys> generate, CancellationToken ct)
    {
        var stored = await FindAsync(ct);
        if (stored is not null)
        {
            return stored;
        }

        var keys = generate();
        try
        {
            await Collection.InsertOneAsync(
                new VapidKeysDocument(FixedId, keys.PublicKey, keys.PrivateKey, keys.CreatedAt), cancellationToken: ct);
            return keys;
        }
        catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return await FindAsync(ct)
                ?? throw new InvalidOperationException("The stored VAPID keys vanished after a duplicate insert.");
        }
    }

    async Task<VapidKeys?> FindAsync(CancellationToken ct)
    {
        var document = await Collection.Find(Builders<VapidKeysDocument>.Filter.Eq(stored => stored.Id, FixedId))
            .FirstOrDefaultAsync(ct);
        return document is null ? null : new VapidKeys(document.PublicKey, document.PrivateKey, document.CreatedAt);
    }

    sealed record VapidKeysDocument(string Id, string PublicKey, string PrivateKey, DateTimeOffset CreatedAt);
}
