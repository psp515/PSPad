using PSPad.Module.Money.Values;

namespace PSPad.App.State.Investments;

public static class InvestmentLinkDay
{
    public static DateOnly DefaultFor(YearMonth month, DateOnly today) => month.LastDay < today ? month.LastDay : today;

    public static bool Offered(YearMonth month, DateOnly today) => month <= YearMonth.Of(today);

    public static bool Allowed(YearMonth month, DateOnly day, DateOnly today) =>
        YearMonth.Of(day) == month && day <= today;
}
