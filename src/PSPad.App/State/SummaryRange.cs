namespace PSPad.App.State;

public sealed record SummaryRange(DateOnly From, DateOnly To)
{
    public static SummaryRange For(SummaryPreset preset, DateOnly today) => preset switch
    {
        SummaryPreset.LastSixMonths => new SummaryRange(
            new DateOnly(today.Year, today.Month, 1).AddMonths(-6), new DateOnly(today.Year, today.Month, 1).AddDays(-1)),
        SummaryPreset.ThisYear => new SummaryRange(new DateOnly(today.Year, 1, 1), today),
        SummaryPreset.Last12Months => new SummaryRange(new DateOnly(today.Year, today.Month, 1).AddMonths(-11), today),
        SummaryPreset.LastYear => new SummaryRange(new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "A custom range comes from the picker.")
    };

    public static SummaryRange? Picked(DateTime? start, DateTime? end)
    {
        if (start is not { } first || end is not { } last)
        {
            return null;
        }

        var from = DateOnly.FromDateTime(first);
        var to = DateOnly.FromDateTime(last);
        return from <= to ? new SummaryRange(from, to) : new SummaryRange(to, from);
    }
}
