namespace PSPad.Module.Statistics;

public interface ILabelStore
{
    Task SaveAsync(StatisticsLabel label, CancellationToken ct);
    Task<StatisticsLabel?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<StatisticsLabel>> AllAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<StatisticsLabel>> ByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
