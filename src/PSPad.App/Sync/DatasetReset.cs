using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.App.Statistics;

namespace PSPad.App.Sync;

public sealed class DatasetReset(IReplica replica, StatisticsCache? statistics = null, ISnapshotCache? snapshots = null)
{
    public async Task ResetAsync(Guid datasetId)
    {
        var owner = await replica.OwnerAsync();

        await replica.ClearAsync();

        if (owner is { } user)
        {
            await replica.SetOwnerAsync(user);
        }

        await replica.SetDatasetIdAsync(datasetId);

        if (statistics is not null)
        {
            await statistics.ClearAsync();
        }

        if (snapshots is not null)
        {
            await snapshots.ClearAsync();
        }
    }
}
