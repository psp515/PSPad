using Microsoft.Extensions.Logging;
using PSPad.App.State;
using PSPad.App.State.Investments;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Statistics;

namespace PSPad.App.Auth;

public sealed class LocalDeviceData(
    ILocalSessionStore sessions,
    IReplica replica,
    IOutbox outbox,
    StatisticsCache statistics,
    ISnapshotCache snapshots,
    IQuoteStore quotes,
    ILogger<LocalDeviceData> logger)
{
    public async Task ClearAsync(string occasion)
    {
        await ClearOneAsync("Replica", replica.ClearAsync, occasion);
        await ClearOneAsync("Outbox", outbox.ClearAsync, occasion);
        await ClearOneAsync("Statistics cache", statistics.ClearAsync, occasion);
        await ClearOneAsync("Snapshot cache", snapshots.ClearAsync, occasion);
        await ClearOneAsync("Quote cache", quotes.ClearAsync, occasion);
        await ClearOneAsync("Session", sessions.ClearAsync, occasion);
    }

    async Task ClearOneAsync(string store, Func<Task> clear, string occasion)
    {
        try
        {
            await clear();
        }
        catch (Exception exception)
        {
            logger.LogWarning("{Store} clear on {Occasion} failed: {Reason}", store, occasion, exception.Message);
        }
    }
}
