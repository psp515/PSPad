using PSPad.Abstractions;

namespace PSPad.Module.Statistics;

public sealed class StatisticsReplay(
    IEventLog log, IProjectionMarker marker, IEnumerable<IDomainEventHandler> handlers)
    : IDomainEventReplay
{
    const int Batch = 500;

    public async Task CatchUpAsync(CancellationToken ct)
    {
        var subscribers = handlers.ToArray();
        var from = await marker.ReadAsync(ct);

        while (true)
        {
            var batch = await log.ReadForwardAsync(from, Batch, ct);

            if (batch.Count == 0)
            {
                return;
            }

            foreach (var recorded in batch)
            {
                var @event = DomainEventCatalogue.Deserialize(recorded);

                if (@event is not null)
                {
                    foreach (var subscriber in subscribers)
                    {
                        await subscriber.HandleAsync(new DomainEventEnvelope(recorded.Seq, @event), ct);
                    }
                }

                from = recorded.Seq;
            }

            await marker.WriteAsync(from, ct);
        }
    }
}
