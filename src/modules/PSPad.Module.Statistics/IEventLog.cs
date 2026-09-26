using PSPad.Contracts;

namespace PSPad.Module.Statistics;

public interface IEventLog
{
    Task<IReadOnlyList<RecordedEvent>> ReadForwardAsync(
        long afterSeq, int limit, CancellationToken ct);
}
