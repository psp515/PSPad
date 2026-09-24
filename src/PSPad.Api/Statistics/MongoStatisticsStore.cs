using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

namespace PSPad.Api.Statistics;

public sealed class MongoStatisticsStore(MongoContext context) : IStatisticsStore
{
    static readonly RecordKind[] Lifecycle =
        [RecordKind.Created, RecordKind.Reopened, RecordKind.Completed, RecordKind.Deleted];

    IMongoCollection<StatisticsRecord> Records => context.Collection<StatisticsRecord>("statistics_records");

    public Task SaveAsync(StatisticsRecord record, CancellationToken ct) =>
        Records.ReplaceOneAsync(
            Builders<StatisticsRecord>.Filter.Eq(stored => stored.Id, record.Id),
            record,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public async Task<int> CountCompletionsBeforeAsync(
        Guid userId, Guid taskId, long seq, CancellationToken ct) =>
        (int)await Records.CountDocumentsAsync(
            Builders<StatisticsRecord>.Filter.Eq(record => record.UserId, userId) &
            Builders<StatisticsRecord>.Filter.Eq(record => record.TaskId, taskId) &
            Builders<StatisticsRecord>.Filter.Eq(record => record.Kind, RecordKind.Completed) &
            Builders<StatisticsRecord>.Filter.Lt(record => record.Id, seq),
            cancellationToken: ct);

    public async Task<IReadOnlyList<StatisticsRecord>> PageAsync(
        Guid userId, long? before, int limit, CancellationToken ct)
    {
        var filter = Builders<StatisticsRecord>.Filter.Eq(record => record.UserId, userId);

        if (before is not null)
        {
            filter &= Builders<StatisticsRecord>.Filter.Lt(record => record.Id, before.Value);
        }

        return await Records
            .Find(filter)
            .SortByDescending(record => record.Id)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StatisticsRecord>> SinceAsync(
        Guid userId, DateTimeOffset from, CancellationToken ct) =>
        await Records
            .Find(
                Builders<StatisticsRecord>.Filter.Eq(record => record.UserId, userId) &
                Builders<StatisticsRecord>.Filter.Gte(record => record.At, from))
            .SortBy(record => record.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> OpenTaskIdsBeforeAsync(
        Guid userId, DateTimeOffset from, CancellationToken ct)
    {
        var earlier = await Records
            .Find(
                Builders<StatisticsRecord>.Filter.Eq(record => record.UserId, userId) &
                Builders<StatisticsRecord>.Filter.Lt(record => record.At, from) &
                Builders<StatisticsRecord>.Filter.In(record => record.Kind, Lifecycle))
            .SortBy(record => record.Id)
            .Project(record => new { record.TaskId, record.Kind })
            .ToListAsync(ct);

        var open = new HashSet<Guid>();

        foreach (var record in earlier)
        {
            if (record.Kind is RecordKind.Created or RecordKind.Reopened)
            {
                open.Add(record.TaskId);
            }
            else
            {
                open.Remove(record.TaskId);
            }
        }

        return open;
    }
}
