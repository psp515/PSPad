using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

namespace PSPad.Api.Statistics;

public sealed class MongoLabelStore(MongoContext context) : ILabelStore
{
    IMongoCollection<StatisticsLabel> Labels => context.Collection<StatisticsLabel>("statistics_labels");

    public Task SaveAsync(StatisticsLabel label, CancellationToken ct) =>
        Labels.ReplaceOneAsync(
            Builders<StatisticsLabel>.Filter.Eq(stored => stored.Id, label.Id),
            label,
            new ReplaceOptions { IsUpsert = true },
            ct);

    public async Task<StatisticsLabel?> FindAsync(Guid id, CancellationToken ct) =>
        await Labels
            .Find(Builders<StatisticsLabel>.Filter.Eq(label => label.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StatisticsLabel>> AllAsync(Guid userId, CancellationToken ct) =>
        await Labels
            .Find(Builders<StatisticsLabel>.Filter.Eq(label => label.UserId, userId))
            .ToListAsync(ct);
}
