using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record MonthTotals(
    decimal Income, decimal Expenses, IReadOnlyList<CategoryTotal> ExpensesByCategory, IReadOnlyList<EntryDay> Days)
{
    public decimal Net => Income - Expenses;

    public static MonthTotals For(IEnumerable<MoneyEntry> entries, YearMonth month)
    {
        var inMonth = entries.Where(entry => !entry.Deleted && month.Contains(entry.Date)).ToArray();
        var expenses = inMonth.Where(entry => entry.Kind == CategoryKind.Expense).ToArray();

        var byCategory = expenses
            .GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CategoryTotal(group.First().Category, group.Sum(entry => entry.Money.InPln)))
            .OrderByDescending(total => total.Pln)
            .ThenBy(total => total.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var days = inMonth
            .GroupBy(entry => entry.Date)
            .OrderByDescending(group => group.Key)
            .Select(group => new EntryDay(group.Key, [.. group.OrderByDescending(entry => entry.RecordedAt)]))
            .ToArray();

        return new MonthTotals(
            inMonth.Where(entry => entry.Kind == CategoryKind.Income).Sum(entry => entry.Money.InPln),
            expenses.Sum(entry => entry.Money.InPln),
            byCategory,
            days);
    }
}
