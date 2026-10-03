using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Api.Snapshots;

public sealed class MongoSnapshotVisitStore(MongoContext context) : ISnapshotVisitStore
{
    const string CollectionName = "snapshot_visits";

    static MongoSnapshotVisitStore()
    {
        BsonClassMap.RegisterClassMap<SnapshotVisit>(map =>
        {
            map.AutoMap();
            map.MapMember(visit => visit.ExpiresAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        });
    }

    IMongoCollection<SnapshotVisit> Collection => context.Collection<SnapshotVisit>(CollectionName);

    public Task SaveAsync(SnapshotVisit visit, CancellationToken ct) =>
        Collection.ReplaceOneAsync(
            Builders<SnapshotVisit>.Filter.Eq(document => document.Id, visit.Id),
            visit,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public async Task<IReadOnlyList<SnapshotVisit>> ForUserAsync(Guid userId, CancellationToken ct) =>
        await Collection.Find(Builders<SnapshotVisit>.Filter.Eq(document => document.UserId, userId))
            .ToListAsync(ct);
}
