using PSPad.Abstractions;

namespace PSPad.App.State;

public interface IReplica
{
    Task<T?> LoadAsync<T>(Guid id) where T : Aggregate;

    Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate;

    Task SaveAsync(Aggregate aggregate);

    Task<long> MarkerAsync();

    Task SetMarkerAsync(long marker);
}
