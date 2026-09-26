namespace PSPad.Module.Statistics;

public interface IStatisticsStore
{
    Task SaveAsync(StatisticsRecord record, CancellationToken ct);
    Task<int> CountCompletionsBeforeAsync(Guid userId, Guid taskId, long seq, CancellationToken ct);
    Task<IReadOnlyList<StatisticsRecord>> PageAsync(Guid userId, long? before, int limit, CancellationToken ct);
    Task<IReadOnlyList<StatisticsRecord>> SinceAsync(Guid userId, DateTimeOffset from, CancellationToken ct);
    Task<IReadOnlySet<Guid>> OpenTaskIdsBeforeAsync(Guid userId, DateTimeOffset from, CancellationToken ct);
}
