using PSPad.Module.Sharing.Ports;
using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Module.Sharing.Tests.Fakes;

public sealed class InMemorySnapshotStore : ISnapshotStore
{
    readonly Dictionary<Guid, ListSnapshot> _snapshots = [];

    public Task SaveAsync(ListSnapshot snapshot, CancellationToken ct)
    {
        _snapshots[snapshot.Id] = snapshot;
        return Task.CompletedTask;
    }

    public Task<ListSnapshot?> FindByTokenAsync(string token, CancellationToken ct) =>
        Task.FromResult(_snapshots.Values.FirstOrDefault(snapshot => snapshot.Token == token));

    public Task<ListSnapshot?> FindAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_snapshots.GetValueOrDefault(id));

    public Task<IReadOnlyList<ListSnapshot>> ForListAsync(Guid userId, Guid listId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ListSnapshot>>(
            _snapshots.Values.Where(snapshot => snapshot.UserId == userId && snapshot.ListId == listId).ToArray());

    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _snapshots.Remove(id);
        return Task.CompletedTask;
    }
}
