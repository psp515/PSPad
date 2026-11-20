using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Reminders;

namespace PSPad.Api.Reminders;

public sealed class MongoReminderClaims(MongoContext context) : IReminderClaims
{
    public const string CollectionName = "sentreminders";

    static MongoReminderClaims()
    {
        BsonClassMap.RegisterClassMap<ReminderClaim>(map =>
        {
            map.AutoMap();
            map.MapIdMember(claim => claim.Id);
            map.MapMember(claim => claim.ClaimedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
            map.MapMember(claim => claim.ExpiresAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        });
    }

    IMongoCollection<ReminderClaim> Collection => context.Collection<ReminderClaim>(CollectionName);

    public async Task<bool> TryClaimAsync(ReminderClaim claim, CancellationToken ct)
    {
        try
        {
            await Collection.InsertOneAsync(claim, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public Task UpdateAsync(string id, ClaimState state, int attempts, CancellationToken ct) =>
        Collection.UpdateOneAsync(
            Builders<ReminderClaim>.Filter.Eq(claim => claim.Id, id),
            Builders<ReminderClaim>.Update.Set(claim => claim.State, state).Set(claim => claim.Attempts, attempts),
            cancellationToken: ct);

    public async Task<IReadOnlyList<ReminderClaim>> RetriesSinceAsync(DateTimeOffset since, CancellationToken ct) =>
        await Collection.Find(
                Builders<ReminderClaim>.Filter.Eq(claim => claim.State, ClaimState.Retry) &
                Builders<ReminderClaim>.Filter.Gte(claim => claim.ClaimedAt, since))
            .ToListAsync(ct);

    public Task PruneBeforeAsync(DateTimeOffset before, CancellationToken ct) =>
        Collection.DeleteManyAsync(Builders<ReminderClaim>.Filter.Lt(claim => claim.ClaimedAt, before), ct);
}
