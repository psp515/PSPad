using PSPad.Abstractions;

namespace PSPad.App.State.Replica;

public sealed class InMemoryReplica : IReplica
{
    readonly Dictionary<Guid, Aggregate> _documents = [];
    long _marker;
    Guid? _owner;
    string? _collectionsFingerprint;

    public Task<T?> LoadAsync<T>(Guid id) where T : Aggregate =>
        Task.FromResult(_documents.GetValueOrDefault(id) as T);

    public Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate =>
        Task.FromResult<IReadOnlyList<T>>(
            _documents.Values.OfType<T>().Where(document => document.UserId == userId).ToArray());

    public Task SaveAsync(Aggregate aggregate)
    {
        _documents[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task<long> MarkerAsync() => Task.FromResult(_marker);

    public Task SetMarkerAsync(long marker)
    {
        _marker = marker;
        return Task.CompletedTask;
    }

    public Task<string?> CollectionsFingerprintAsync() => Task.FromResult(_collectionsFingerprint);

    public Task SetCollectionsFingerprintAsync(string fingerprint)
    {
        _collectionsFingerprint = fingerprint;
        return Task.CompletedTask;
    }

    public Task<Guid?> OwnerAsync() => Task.FromResult(_owner);

    public Task SetOwnerAsync(Guid userId)
    {
        _owner = userId;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _documents.Clear();
        _marker = 0;
        _owner = null;
        _collectionsFingerprint = null;
        return Task.CompletedTask;
    }
}
