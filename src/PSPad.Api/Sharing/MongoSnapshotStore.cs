using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Api.Snapshots;

public sealed class MongoSnapshotStore(MongoContext context) : ISnapshotStore
{
    const string CollectionName = "list_snapshots";

    static MongoSnapshotStore()
    {
        BsonClassMap.RegisterClassMap<ListSnapshot>(map =>
        {
            map.AutoMap();
            map.MapMember(snapshot => snapshot.ExpiresAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        });
    }

    IMongoCollection<ListSnapshot> Collection => context.Collection<ListSnapshot>(CollectionName);

    public Task SaveAsync(ListSnapshot snapshot, CancellationToken ct) =>
        Collection.ReplaceOneAsync(
            Builders<ListSnapshot>.Filter.Eq(document => document.Id, snapshot.Id),
            snapshot,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public async Task<ListSnapshot?> FindByTokenAsync(string token, CancellationToken ct) =>
        await Collection.Find(Builders<ListSnapshot>.Filter.Eq(document => document.Token, token))
            .FirstOrDefaultAsync(ct);

    public async Task<ListSnapshot?> FindAsync(Guid id, CancellationToken ct) =>
        await Collection.Find(Builders<ListSnapshot>.Filter.Eq(document => document.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ListSnapshot>> ForListAsync(Guid userId, Guid listId, CancellationToken ct) =>
        await Collection.Find(
                Builders<ListSnapshot>.Filter.Eq(document => document.UserId, userId) &
                Builders<ListSnapshot>.Filter.Eq(document => document.ListId, listId))
            .ToListAsync(ct);

    public Task DeleteAsync(Guid id, CancellationToken ct) =>
        Collection.DeleteOneAsync(Builders<ListSnapshot>.Filter.Eq(document => document.Id, id), ct);
}
