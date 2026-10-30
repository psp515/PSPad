namespace PSPad.App.State.Investments;

public static class ChartRanges
{
    public static DateOnly? Start(ChartRange range, DateOnly today) => range switch
    {
        ChartRange.ThreeMonths => today.AddMonths(-3),
        ChartRange.OneYear => today.AddYears(-1),
        _ => null
    };

    public static IReadOnlyList<T> Within<T>(IReadOnlyList<T> points, Func<T, DateOnly> dayOf, ChartRange range, DateOnly today) =>
        Start(range, today) is { } start ? [.. points.Where(point => dayOf(point) >= start)] : points;
}
