using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tests.Fakes;

public sealed class FakeDocumentStore<T> : IDocumentStore<T> where T : Aggregate
{
    readonly Dictionary<Guid, T> _documents = [];

    public void Seed(T document) => _documents[document.Id] = document;

    public Task<T?> LoadAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_documents.GetValueOrDefault(id));

    public Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<T>>(
            _documents.Values.Where(document => document.UserId == userId).ToArray());
}
