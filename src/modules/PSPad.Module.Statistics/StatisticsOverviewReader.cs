using PSPad.Contracts;

namespace PSPad.Module.Statistics;

public sealed class StatisticsOverviewReader(IStatisticsStore store, ILabelStore labels)
{
    const int DefaultRange = 30;

    static readonly int[] OfferedRanges = [30, 90, 365];

    public async Task<StatisticsOverview> ReadAsync(
        Guid userId, DateOnly today, int days, TimeZoneInfo zone, CancellationToken ct)
    {
        var range = OfferedRanges.Contains(days) ? days : DefaultRange;
        var from = StartOfDay(today.AddDays(1 - range), zone);

        // Both reads take the same instant so every record falls on exactly one side of it:
        // inside the window it is charted, outside it folds into the outstanding line's start.
        var records = await store.SinceAsync(userId, from, ct);
        var openAtStart = await store.OpenTaskIdsBeforeAsync(userId, from, ct);
        var known = await labels.AllAsync(userId, ct);

        var outstanding = StatisticsCharts.Outstanding(records, today, range, zone, openAtStart);

        return new StatisticsOverview(
            Tiles(StatisticsCharts.Tiles(records, outstanding, today, zone)),
            StatisticsCharts.Completions(records, today, range, zone).Select(Completions).ToArray(),
            StatisticsCharts.Opened(records, today, range, zone).Select(Count).ToArray(),
            outstanding.Select(Count).ToArray(),
            StatisticsCharts.ByGoal(records, known).Select(Goal).ToArray(),
            StatisticsCharts.Heatmap(records, today, range, zone).Select(Count).ToArray());
    }

    static StatisticsTilesView Tiles(StatisticsTiles tiles) =>
        new(tiles.DoneToday, tiles.OpenedToday, tiles.DoneThisWeek, tiles.NetChange);

    static DailyCompletionsView Completions(DailyCompletions point) =>
        new(point.Day, point.Planned, point.Unplanned);

    static DailyCountView Count(DailyCount point) => new(point.Day, point.Count);

    static GoalTotalView Goal(GoalTotal bar) => new(bar.GoalId, bar.Name, bar.Count);

    static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
