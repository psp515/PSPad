using PSPad.Contracts;

namespace PSPad.App.Statistics;

public interface IStatisticsSource
{
    Task<StatisticsOverview?> OverviewAsync(int days);

    Task<IReadOnlyList<StatisticsRecordView>> RecordsAsync(long? before, int limit);
}
