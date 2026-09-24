using PSPad.Contracts;

namespace PSPad.Module.Statistics;

public sealed class HistoryReader(IEventLog log)
{
    const int DefaultLimit = 50;
    const int MaximumLimit = 200;

    public async Task<IReadOnlyList<HistoryEntry>> ReadAsync(
        Guid userId, long? before, int limit, CancellationToken ct)
    {
        var page = limit switch
        {
            < 1 => DefaultLimit,
            > MaximumLimit => MaximumLimit,
            _ => limit
        };

        var events = await log.ReadAsync(userId, before, page, ct);

        return events
            .Select(recorded => new HistoryEntry(
                recorded.Seq,
                recorded.At,
                recorded.AggregateType,
                recorded.AggregateId,
                HistoryDescriptions.For(recorded.Type)))
            .ToArray();
    }
}
