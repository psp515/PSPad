using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record MonthPace(
    YearMonth Month,
    int Day,
    int DaysInMonth,
    MonthTotals Totals,
    UsualMonth Usual,
    IReadOnlyList<CategoryPace> Categories)
{
    public decimal ShareOfMonthPassed => (decimal)Day / DaysInMonth;

    public decimal? ExpectedIncomeByNow => Usual.IsEmpty ? null : ByNow(Usual.Income);

    public decimal? ExpectedExpensesByNow => Usual.IsEmpty ? null : ByNow(Usual.Expenses);

    public decimal? ShareOfUsualSpent => Usual.IsEmpty || Usual.Expenses == 0 ? null : Totals.Expenses / Usual.Expenses;

    public static MonthPace For(IEnumerable<MoneyEntry> entries, YearMonth month, DateOnly today)
    {
        var all = entries.ToArray();
        var totals = MonthTotals.For(all, month);
        var usual = UsualMonth.For(all, month);
        var daysInMonth = month.LastDay.Day;
        var day = month.Contains(today) ? today.Day : month < YearMonth.Of(today) ? daysInMonth : 0;
        var share = (decimal)day / daysInMonth;

        var names = totals.ExpensesByCategory.Select(total => total.Category)
            .Concat(usual.ExpenseCategories.Select(category => category.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var categories = names
            .Select(name =>
            {
                var spent = totals.ExpensesByCategory
                    .FirstOrDefault(total => string.Equals(total.Category, name, StringComparison.OrdinalIgnoreCase))?.Pln ?? 0m;
                var average = usual.Expense(name)?.Average;
                return new CategoryPace(name, spent, average is { } value ? Round(value * share) : null, average);
            })
            .OrderByDescending(category => category.Spent)
            .ThenByDescending(category => category.UsualMonth ?? 0m)
            .ThenBy(category => category.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MonthPace(month, day, daysInMonth, totals, usual, categories);
    }

    decimal ByNow(decimal usual) => Round(usual * ShareOfMonthPassed);

    static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
