using PSPad.Module.Money.Values;

namespace PSPad.App.State;

public static class HoldingRateDay
{
    public static DateOnly Of(YearMonth month, DateOnly today) => month.LastDay < today ? month.LastDay : today;
}
