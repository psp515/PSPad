using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Module.Sharing.Ports;

public interface ISnapshotStore
{
    Task SaveAsync(ListSnapshot snapshot, CancellationToken ct);
    Task<ListSnapshot?> FindByTokenAsync(string token, CancellationToken ct);
    Task<ListSnapshot?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ListSnapshot>> ForListAsync(Guid userId, Guid listId, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
