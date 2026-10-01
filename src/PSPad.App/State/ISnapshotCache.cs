using PSPad.Contracts;

namespace PSPad.App.State;

public interface ISnapshotCache
{
    Task SaveAsync(string token, SnapshotView snapshot);

    Task<SnapshotView?> GetAsync(string token);

    Task<IReadOnlyList<(string Token, SnapshotView Snapshot)>> AllAsync();

    Task PruneAsync(DateTimeOffset now);

    Task ClearAsync();
}
