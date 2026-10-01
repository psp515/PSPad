using PSPad.Contracts;

namespace PSPad.App.State;

public sealed class InMemorySnapshotCache : ISnapshotCache
{
    readonly Dictionary<string, SnapshotView> _snapshots = [];

    public Task SaveAsync(string token, SnapshotView snapshot)
    {
        _snapshots[token] = snapshot;
        return Task.CompletedTask;
    }

    public Task<SnapshotView?> GetAsync(string token) =>
        Task.FromResult(_snapshots.GetValueOrDefault(token));

    public Task<IReadOnlyList<(string Token, SnapshotView Snapshot)>> AllAsync() =>
        Task.FromResult<IReadOnlyList<(string Token, SnapshotView Snapshot)>>(
            _snapshots.Select(pair => (pair.Key, pair.Value)).ToArray());

    public Task PruneAsync(DateTimeOffset now)
    {
        foreach (var token in _snapshots
            .Where(pair => pair.Value.ExpiresAt <= now)
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
