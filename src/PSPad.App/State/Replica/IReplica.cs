using PSPad.Abstractions;

namespace PSPad.App.State.Replica;

public interface IReplica
{
    Task<T?> LoadAsync<T>(Guid id) where T : Aggregate;

    Task<Aggregate?> LoadAsync(Type type, Guid id);

    // A replica holds one user's whole visible world (ReplicaOwnership), shared lists included.
    Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate;

    Task SaveAsync(Aggregate aggregate);

    // The check and the save are one replica transaction: a command's commit landing between them would be undone by the save.
    Task<bool> SaveUnlessNewerAsync(Aggregate aggregate);

    Task RemoveAsync(Guid id);

    Task<long> MarkerAsync();

    Task SetMarkerAsync(long marker);

    Task<string?> CollectionsFingerprintAsync();

    Task SetCollectionsFingerprintAsync(string fingerprint);

    // Lists and budgets a pull found new and has not yet pulled whole; kept so an interrupted pull still owes them.
    Task<IReadOnlyCollection<Guid>> OwedWholeAsync();

    Task SetOwedWholeAsync(IReadOnlyCollection<Guid> containers);

    Task<DateTimeOffset?> LastSyncedAtAsync();

    Task SetLastSyncedAtAsync(DateTimeOffset at);

    Task<Guid?> OwnerAsync();

    Task SetOwnerAsync(Guid userId);

    Task ClearAsync();
}
