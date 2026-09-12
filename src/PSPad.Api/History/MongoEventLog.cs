using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.History;

namespace PSPad.Api.History;

public sealed class MongoEventLog(MongoContext context) : IEventLog
{
    public async Task<IReadOnlyList<RecordedEvent>> ReadAsync(
        Guid userId, long? before, int limit, CancellationToken ct)
    {
        var filter = Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId);

        if (before is not null)
        {
            filter &= Builders<StoredEvent>.Filter.Lt(entry => entry.Seq, before.Value);
        }

        var events = await context.Collection<StoredEvent>("events")
            .Find(filter)
            .SortByDescending(entry => entry.Seq)
            .Limit(limit)
            .ToListAsync(ct);

        return events
            .Select(entry => new RecordedEvent(
                entry.Seq, entry.AggregateType, entry.AggregateId, entry.Type, entry.Payload, entry.At))
            .ToArray();
    }
}
