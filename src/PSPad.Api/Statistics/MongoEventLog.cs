using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

namespace PSPad.Api.Statistics;

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

        return Project(events);
    }

    public async Task<IReadOnlyList<RecordedEvent>> ReadForwardAsync(
        long afterSeq, int limit, CancellationToken ct)
    {
        var events = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, afterSeq))
            .SortBy(entry => entry.Seq)
            .Limit(limit)
            .ToListAsync(ct);

        return Project(events);
    }

    static RecordedEvent[] Project(IEnumerable<StoredEvent> events) =>
        events
            .Select(entry => new RecordedEvent(
                entry.Seq, entry.AggregateType, entry.AggregateId, entry.Type, entry.Payload, entry.At))
            .ToArray();
}
