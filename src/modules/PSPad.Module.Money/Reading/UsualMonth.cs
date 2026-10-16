using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record UsualMonth(
    YearMonth From,
    YearMonth To,
    int SampledMonths,
    decimal Income,
    decimal Expenses,
    IReadOnlyList<CategoryUsual> ExpenseCategories,
    IReadOnlyList<CategoryUsual> IncomeCategories)
{
    public const int Window = 6;

    public decimal Saved => Income - Expenses;

    public bool IsEmpty => SampledMonths == 0;

    public CategoryUsual? Expense(string category) =>
        ExpenseCategories.FirstOrDefault(usual => string.Equals(usual.Category, category, StringComparison.OrdinalIgnoreCase));

    public static UsualMonth For(IEnumerable<MoneyEntry> entries, YearMonth current)
    {
        var to = current.Previous();
        var from = YearMonth.Of(to.FirstDay.AddMonths(-(Window - 1)));
        var inWindow = entries
            .Where(entry => !entry.Deleted && entry.Date >= from.FirstDay && entry.Date <= to.LastDay)
            .ToArray();
        var sampled = inWindow.Select(entry => YearMonth.Of(entry.Date)).Distinct().Count();

        if (sampled == 0)
        {
            return new UsualMonth(from, to, 0, 0m, 0m, [], []);
        }

        var incomes = inWindow.Where(entry => entry.Kind == CategoryKind.Income).ToArray();
        var expenses = inWindow.Where(entry => entry.Kind == CategoryKind.Expense).ToArray();

        return new UsualMonth(
            from, to, sampled,
            Average(incomes.Sum(entry => entry.Money.InPln), sampled),
            Average(expenses.Sum(entry => entry.Money.InPln), sampled),
            Categories(expenses, sampled),
            Categories(incomes, sampled));
    }

    static CategoryUsual[] Categories(MoneyEntry[] entries, int sampled) =>
    [
        .. entries
            .GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CategoryUsual(
                group.First().Category,
                Average(group.Sum(entry => entry.Money.InPln), sampled),
                group.Select(entry => YearMonth.Of(entry.Date)).Distinct().Count(),
                LowerMedian(group.Select(entry => entry.Date.Day))))
            .OrderByDescending(usual => usual.Average)
            .ThenBy(usual => usual.Category, StringComparer.OrdinalIgnoreCase)
    ];

    static decimal Average(decimal total, int months) => Math.Round(total / months, 2, MidpointRounding.AwayFromZero);

    static int LowerMedian(IEnumerable<int> days)
    {
        var sorted = days.Order().ToArray();
        return sorted[(sorted.Length - 1) / 2];
    }
}
