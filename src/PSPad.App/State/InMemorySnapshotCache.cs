using PSPad.Contracts;

namespace PSPad.App.State;

public sealed class InMemorySnapshotCache : ISnapshotCache
{
    readonly Dictionary<string, (SnapshotView Snapshot, DateTimeOffset OpenedAt)> _snapshots = [];

    public Task SaveAsync(string token, SnapshotView snapshot, DateTimeOffset openedAt)
    {
        _snapshots[token] = (snapshot, openedAt);
        return Task.CompletedTask;
    }

    public Task<SnapshotView?> GetAsync(string token) =>
        Task.FromResult(_snapshots.TryGetValue(token, out var entry) ? entry.Snapshot : null);

    public Task<IReadOnlyList<(string Token, SnapshotView Snapshot, DateTimeOffset OpenedAt)>> AllAsync() =>
        Task.FromResult<IReadOnlyList<(string Token, SnapshotView Snapshot, DateTimeOffset OpenedAt)>>(
            _snapshots.Select(pair => (pair.Key, pair.Value.Snapshot, pair.Value.OpenedAt)).ToArray());

    public Task PruneAsync(DateTimeOffset now)
    {
        foreach (var token in _snapshots
            .Where(pair => pair.Value.Snapshot.ExpiresAt <= now)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _snapshots.Remove(token);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _snapshots.Clear();
        return Task.CompletedTask;
    }
}
