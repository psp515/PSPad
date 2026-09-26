using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Statistics;

namespace PSPad.App.State.Dispatch;

public sealed class ReplicaOwnership(IReplica replica, IOutbox outbox, StatisticsCache cache)
{
    public async Task EnsureCurrentUserAsync(Guid userId)
    {
        var owner = await replica.OwnerAsync();

        // No exception for a never-recorded owner (null): anything queued before this device
        // ever attributed its data to a user (e.g. commands sent during a mid-login race) is
        // exactly the data that must never survive into a real session, ADR-0018's own case.
        if (owner != userId)
        {
            await replica.ClearAsync();
            await outbox.ClearAsync();
            await cache.ClearAsync();
        }

        await replica.SetOwnerAsync(userId);
    }

    // The marker only moves once a pull has landed, so a zero means this device holds none of
    // this user's data yet -- not that it is merely behind.
    public async Task<bool> NothingSyncedYetAsync() => await replica.MarkerAsync() == 0;
}
