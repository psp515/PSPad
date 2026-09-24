namespace PSPad.Module.Statistics;

public interface ITaskSnapshotSource
{
    Task<IReadOnlyDictionary<Guid, TaskSnapshot>> CurrentAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct);
}
