using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Sync;

public sealed class SyncReader(MongoContext context)
{
    static readonly string[] Collections = ["areas", "tasklists", "todotasks", "goals", "inboxes", "users"];

    public async Task<SyncResponse> ReadAsync(Guid userId, long since, CancellationToken ct)
    {
        var events = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId) &
                  Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, since))
            .SortBy(entry => entry.Seq)
            .ToListAsync(ct);

        var documents = new Dictionary<string, JsonElement[]>();

        foreach (var name in Collections)
        {
            var rows = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", new BsonBinaryData(userId, GuidRepresentation.Standard)) &
                      Builders<BsonDocument>.Filter.Gt("seq", since))
                .ToListAsync(ct);

            documents[name] = rows
                .Select(row => JsonSerializer.Deserialize<JsonElement>(row.ToJson()))
                .ToArray();
        }

        var marker = events.Count > 0
            ? events[^1].Seq
            : Math.Max(since, await HighestSeqAsync(userId, ct));

        return new SyncResponse(
            marker,
            documents,
            events.Select(entry => new SyncEvent(
                entry.Seq, entry.AggregateType, entry.AggregateId, entry.Type, entry.Payload, entry.At))
                .ToArray());
    }

    async Task<long> HighestSeqAsync(Guid userId, CancellationToken ct)
    {
        var newest = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId))
            .SortByDescending(entry => entry.Seq)
            .FirstOrDefaultAsync(ct);

        return newest?.Seq ?? 0;
    }
}
