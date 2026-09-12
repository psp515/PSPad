using PSPad.Contracts;

namespace PSPad.Module.History;

public interface IEventLog
{
    Task<IReadOnlyList<RecordedEvent>> ReadAsync(
        Guid userId, long? before, int limit, CancellationToken ct);
}
