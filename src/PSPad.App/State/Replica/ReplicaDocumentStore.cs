using PSPad.Abstractions;

namespace PSPad.App.State.Replica;

public sealed class ReplicaDocumentStore<T>(IReplica replica) : IDocumentStore<T> where T : Aggregate
{
    public Task<T?> LoadAsync(Guid id, CancellationToken ct) => replica.LoadAsync<T>(id);

    public Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct) =>
        replica.LoadAllAsync<T>(userId);
}
