using PSPad.Module.Money.Values;

namespace PSPad.App.State;

public static class EntryDefaultDate
{
    public static DateOnly For(YearMonth? month, DateOnly today) => month switch
    {
        null => today,
        { } shown when shown.Contains(today) => today,
        { } shown when shown < YearMonth.Of(today) => shown.LastDay,
        { } shown => shown.FirstDay
    };
}
