using PSPad.Module.Money.Values;

namespace PSPad.App.State;

public static class MonthWindow
{
    public const int DefaultCount = 12;
    const int FewestChips = 3;
    const double ChipWidth = 56;
    const double ChipGap = 4;

    public static int Fitting(double width) =>
        Math.Max(FewestChips, (int)Math.Floor((width + ChipGap) / (ChipWidth + ChipGap)));

    public static IReadOnlyList<YearMonth> NewestFirst(YearMonth shown, YearMonth current, int count)
    {
        var chips = new List<YearMonth>(count);
        var month = Newest(shown, current, count);
        for (var back = 0; back < count; back++)
        {
            chips.Add(month);
            month = month.Previous();
        }

        return chips;
    }

    static YearMonth Newest(YearMonth shown, YearMonth current, int count)
    {
        if (shown > current)
        {
            return shown;
        }

        var reach = Shift(shown, count - 1);
        return reach < current ? reach : current;
    }

    static YearMonth Shift(YearMonth month, int months) => YearMonth.Of(month.FirstDay.AddMonths(months));
}
