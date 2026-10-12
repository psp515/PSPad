using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Lists;

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

    public async Task<IReadOnlyList<StatisticsLabel>> ByIdsAsync(
        Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var memberLists = await context.Collection<TaskList>()
            .Find(Builders<TaskList>.Filter.Eq("_members.userId", userId))
            .Project(list => list.Id)
            .ToListAsync(ct);

        return await Labels
            .Find(Builders<StatisticsLabel>.Filter.In(label => label.Id, ids) &
                  (Builders<StatisticsLabel>.Filter.Eq(label => label.UserId, userId) |
                   Builders<StatisticsLabel>.Filter.Eq(label => label.Kind, LabelKind.List) &
                   Builders<StatisticsLabel>.Filter.In(label => label.Id, memberLists)))
            .ToListAsync(ct);
    }
}
