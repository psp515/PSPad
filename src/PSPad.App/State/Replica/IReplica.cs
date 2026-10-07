using PSPad.Abstractions;

namespace PSPad.App.State.Replica;

public interface IReplica
{
    Task<T?> LoadAsync<T>(Guid id) where T : Aggregate;

    // A replica holds one user's whole visible world (ReplicaOwnership), shared lists included.
    Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate;

    Task SaveAsync(Aggregate aggregate);

    Task RemoveAsync(Guid id);

    Task<long> MarkerAsync();

    Task SetMarkerAsync(long marker);

    Task<string?> CollectionsFingerprintAsync();

    Task SetCollectionsFingerprintAsync(string fingerprint);

    Task<DateTimeOffset?> LastSyncedAtAsync();

    Task SetLastSyncedAtAsync(DateTimeOffset at);

    Task<Guid?> OwnerAsync();

    Task SetOwnerAsync(Guid userId);

    Task ClearAsync();
}
