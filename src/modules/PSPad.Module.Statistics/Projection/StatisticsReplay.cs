using Microsoft.Extensions.Logging;
using PSPad.Abstractions;
using PSPad.Contracts;

namespace PSPad.Module.Statistics;

public sealed class StatisticsReplay(
    IEventLog log, IProjectionMarker marker, IEnumerable<IDomainEventHandler> handlers,
    ILogger<StatisticsReplay> logger)
    : IDomainEventReplay
{
    const int Batch = 500;

    public async Task CatchUpAsync(CancellationToken ct)
    {
        try
        {
            await ReplayAsync(ct);
        }
        // Replay runs inside a BackgroundService: an escaping exception stops the whole host by default.
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogError(exception, "Statistics replay could not read the event log; statistics stay behind until the next start.");
        }
    }

    async Task ReplayAsync(CancellationToken ct)
    {
        var subscribers = handlers.ToArray();
        var from = await marker.ReadAsync(ct);
        var persisted = from;

        while (true)
        {
            var batch = await log.ReadForwardAsync(from, Batch, ct);

            if (batch.Count == 0)
            {
                return;
            }

            foreach (var recorded in batch)
            {
                if (!await TryApplyAsync(recorded, subscribers, ct))
                {
                    if (from != persisted)
                    {
                        await marker.WriteAsync(from, ct);
                    }

                    return;
                }

                from = recorded.Seq;
            }

            await marker.WriteAsync(from, ct);
            persisted = from;
        }
    }

    async Task<bool> TryApplyAsync(
        RecordedEvent recorded, IDomainEventHandler[] subscribers, CancellationToken ct)
    {
        try
        {
            var @event = DomainEventCatalogue.Deserialize(recorded);

            if (@event is not null)
            {
                foreach (var subscriber in subscribers)
                {
                    await subscriber.HandleAsync(new DomainEventEnvelope(recorded.Seq, @event), ct);
                }
            }

            return true;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Statistics replay stopped at seq {Seq} ({Type}); the marker stays behind it and the next start retries from there.",
                recorded.Seq,
                recorded.Type);

            return false;
        }
    }
}
