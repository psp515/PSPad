namespace PSPad.Contracts;

public sealed record StatisticsOverview(
    StatisticsTilesView Tiles,
    IReadOnlyList<DailyCompletionsView> Completions,
    IReadOnlyList<DailyCountView> Opened,
    IReadOnlyList<DailyCountView> Outstanding,
    IReadOnlyList<GoalTotalView> ByGoal,
    IReadOnlyList<DailyCountView> Heatmap);
