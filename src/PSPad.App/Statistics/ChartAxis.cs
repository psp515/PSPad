namespace PSPad.App.Statistics;

public static class ChartAxis
{
    const int TargetLabelCount = 7;
    const int TargetYAxisTicks = 5;
    public const int MaxTicks = 6;

    public static string[] Labels<T>(IReadOnlyList<T> points, Func<T, DateOnly> dayOf, int? formatOverRange = null)
    {
        var count = points.Count;

        if (count == 0)
        {
            return [];
        }

        var format = (formatOverRange ?? count) > 90 ? "MMM yyyy" : "MM-dd";
        var step = Math.Max(1, (int)Math.Ceiling(count / (double)TargetLabelCount));
        var labels = new string[count];

        for (var index = 0; index < count; index++)
        {
            var keep = index % step == 0 || index == count - 1;
            labels[index] = keep ? dayOf(points[index]).ToString(format) : "";
        }

        return labels;
    }

    public static int Step(double max)
    {
        if (max <= 0)
        {
            return 1;
        }

        var rawStep = max / TargetYAxisTicks;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        var niceNormalized = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        return Math.Max(1, (int)Math.Round(niceNormalized * magnitude));
    }
}
