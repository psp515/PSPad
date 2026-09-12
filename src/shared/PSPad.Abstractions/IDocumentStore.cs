namespace PSPad.Abstractions;

public interface IDocumentStore<T> where T : Aggregate
{
    Task<T?> LoadAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct);
}
