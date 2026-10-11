using PSPad.Abstractions;

namespace PSPad.Module.Money.Tests.Fakes;

public sealed class FakeDocumentStore<T> : IDocumentStore<T> where T : Aggregate
{
    readonly Dictionary<Guid, T> _documents = [];

    public int Loads { get; private set; }

    public void Seed(T document) => _documents[document.Id] = document;

    public Task<T?> LoadAsync(Guid id, CancellationToken ct)
    {
        Loads++;
        return Task.FromResult(_documents.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct)
    {
        Loads++;
        return Task.FromResult<IReadOnlyList<T>>(
            _documents.Values.Where(document => document.UserId == userId).ToArray());
    }
}
