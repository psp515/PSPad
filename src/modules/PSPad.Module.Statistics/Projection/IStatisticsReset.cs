namespace PSPad.Module.Statistics;

public interface IStatisticsReset
{
    Task ClearAsync(CancellationToken ct);
}
