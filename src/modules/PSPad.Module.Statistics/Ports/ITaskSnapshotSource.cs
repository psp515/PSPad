namespace PSPad.Module.Statistics;

public interface ITaskSnapshotSource
{
    Task<IReadOnlyDictionary<Guid, TaskSnapshot>> CurrentAsync(
        Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct);
}
