using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

namespace PSPad.Api.Statistics;

public sealed class MongoInboxRecordStore(MongoContext context) : IInboxRecordStore
{
    IMongoCollection<InboxRecord> Records =>
        context.Collection<InboxRecord>("statistics_inbox_records");

    public Task SaveAsync(InboxRecord record, CancellationToken ct) =>
        Records.ReplaceOneAsync(
            Builders<InboxRecord>.Filter.Eq(stored => stored.Id, record.Id),
            record,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public async Task<IReadOnlyList<InboxRecord>> SinceAsync(
        Guid userId, DateTimeOffset from, CancellationToken ct) =>
        await Records
            .Find(
                Builders<InboxRecord>.Filter.Eq(record => record.UserId, userId) &
                Builders<InboxRecord>.Filter.Gte(record => record.At, from))
            .SortBy(record => record.Id)
            .ToListAsync(ct);
}
