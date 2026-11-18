using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Reminders;

namespace PSPad.Api.Reminders;

public sealed class MongoPushSubscriptions(MongoContext context) : IPushSubscriptionStore
{
    public const string CollectionName = "pushsubscriptions";

    static MongoPushSubscriptions()
    {
        BsonClassMap.RegisterClassMap<PushSubscriptionRecord>(map =>
        {
            map.AutoMap();
            map.MapIdMember(subscription => subscription.Id);
            map.MapMember(subscription => subscription.CreatedAt)
                .SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
            map.MapMember(subscription => subscription.LastSuccessAt)
                .SetSerializer(new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
        });
    }

    IMongoCollection<PushSubscriptionRecord> Collection => context.Collection<PushSubscriptionRecord>(CollectionName);

    static FilterDefinition<PushSubscriptionRecord> ByEndpoint(string endpoint) =>
        Builders<PushSubscriptionRecord>.Filter.Eq(subscription => subscription.Endpoint, endpoint);

    public async Task<IReadOnlyList<PushSubscriptionRecord>> ForUserAsync(Guid userId, CancellationToken ct) =>
        await Collection.Find(Builders<PushSubscriptionRecord>.Filter.Eq(subscription => subscription.UserId, userId))
            .ToListAsync(ct);

    public async Task<PushSubscriptionRecord?> FindAsync(string endpoint, CancellationToken ct) =>
        await Collection.Find(ByEndpoint(endpoint)).FirstOrDefaultAsync(ct);

    public Task SaveAsync(PushSubscriptionRecord subscription, CancellationToken ct) =>
        Collection.ReplaceOneAsync(ByEndpoint(subscription.Endpoint), subscription, new ReplaceOptions { IsUpsert = true }, ct);

    public Task DeleteAsync(string endpoint, CancellationToken ct) =>
        Collection.DeleteOneAsync(ByEndpoint(endpoint), ct);

    public Task MarkDeliveredAsync(string endpoint, DateTimeOffset at, CancellationToken ct) =>
        Collection.UpdateOneAsync(
            ByEndpoint(endpoint),
            Builders<PushSubscriptionRecord>.Update.Set(subscription => subscription.LastSuccessAt, at),
            cancellationToken: ct);
}
