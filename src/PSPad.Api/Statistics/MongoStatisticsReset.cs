using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

namespace PSPad.Api.Statistics;

public sealed class MongoStatisticsReset(MongoContext context) : IStatisticsReset
{
    public async Task ClearAsync(CancellationToken ct)
    {
        await context.Database.DropCollectionAsync("statistics_records", ct);
        await context.Database.DropCollectionAsync("statistics_inbox_records", ct);
        await context.Database.DropCollectionAsync("statistics_labels", ct);
        await MongoIndexes.EnsureStatisticsAsync(context, ct);
    }
}
