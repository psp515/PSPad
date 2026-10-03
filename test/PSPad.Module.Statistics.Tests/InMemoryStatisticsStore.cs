namespace PSPad.Module.Statistics.Tests;

public sealed class InMemoryStatisticsStore : IStatisticsStore
{
    public List<StatisticsRecord> Records { get; } = [];

    public Task SaveAsync(StatisticsRecord record, CancellationToken ct)
    {
        Records.RemoveAll(existing => existing.Id == record.Id);
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<int> CountCompletionsBeforeAsync(Guid userId, Guid taskId, long seq, CancellationToken ct) =>
        Task.FromResult(Records.Count(record =>
            record.UserId == userId && record.TaskId == taskId &&
            record.Kind == RecordKind.Completed && record.Seq < seq));

    public Task<IReadOnlyList<StatisticsRecord>> PageAsync(Guid userId, long? before, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StatisticsRecord>>(Records);

    public Task<IReadOnlyList<StatisticsRecord>> SinceAsync(Guid userId, DateTimeOffset from, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StatisticsRecord>>(Records);

    public Task<IReadOnlySet<Guid>> OpenTaskIdsBeforeAsync(Guid userId, DateTimeOffset from, CancellationToken ct) =>
        throw new NotSupportedException();
}
