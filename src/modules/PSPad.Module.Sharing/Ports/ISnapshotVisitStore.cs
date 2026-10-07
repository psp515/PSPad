using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Module.Sharing.Ports;

public interface ISnapshotVisitStore
{
    Task SaveAsync(SnapshotVisit visit, CancellationToken ct);
    Task<IReadOnlyList<SnapshotVisit>> ForUserAsync(Guid userId, CancellationToken ct);
}
