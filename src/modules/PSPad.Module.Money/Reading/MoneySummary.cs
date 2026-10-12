using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record MoneySummary(
    decimal Income,
    decimal Expenses,
    IReadOnlyList<YearMonth> Months,
    IReadOnlyList<CumulativeMonth> Cumulative,
    IReadOnlyList<CategorySeries> ExpensesByMonth,
    IReadOnlyList<NetWorthMonth> NetWorth,
    IReadOnlyList<CategoryTotal> Ranking,
    BalanceSnapshot? Latest)
{
    public const int TopCategories = 4;
    public const string Other = "Other";

    public decimal Saved => Income - Expenses;

    public decimal? SavingsRate => Income == 0 ? null : Saved / Income;

    public decimal? LatestNetWorth => Latest is null ? null : BalanceTotals.NetWorthOf(Latest);

    public static MoneySummary For(
        IEnumerable<MoneyEntry> entries, IEnumerable<BalanceSnapshot> snapshots, DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, "A summary range cannot end before it starts.");
        }

        var months = MonthsBetween(YearMonth.Of(from), YearMonth.Of(to));
        var inRange = entries.Where(entry => !entry.Deleted && entry.Date >= from && entry.Date <= to).ToArray();
        var incomes = inRange.Where(entry => entry.Kind == CategoryKind.Income).ToArray();
        var expenses = inRange.Where(entry => entry.Kind == CategoryKind.Expense).ToArray();
        var ranking = CategoryTotal.RankingOf(expenses);
        var live = snapshots.Where(snapshot => !snapshot.Deleted).ToArray();

        return new MoneySummary(
            incomes.Sum(entry => entry.Money.InPln),
            expenses.Sum(entry => entry.Money.InPln),
            months,
            CumulativeOf(months, incomes, expenses),
            ByMonth(months, expenses, ranking),
            [.. months.Select(month => NetWorthMonth.Of(month, live.FirstOrDefault(snapshot => snapshot.Month == month)))],
            ranking,
            BalanceTotals.Latest(live, YearMonth.Of(to)));
    }

    static YearMonth[] MonthsBetween(YearMonth first, YearMonth last)
    {
        var months = new List<YearMonth>();
        for (var month = first; month <= last; month = month.Next())
        {
            months.Add(month);
        }

        return [.. months];
    }

    static CumulativeMonth[] CumulativeOf(IReadOnlyList<YearMonth> months, MoneyEntry[] incomes, MoneyEntry[] expenses)
    {
        var incomePerMonth = PerMonth(months, incomes);
        var spentPerMonth = PerMonth(months, expenses);
        var cumulative = new CumulativeMonth[months.Count];
        var income = 0m;
        var spent = 0m;

        for (var index = 0; index < months.Count; index++)
        {
            income += incomePerMonth[index];
            spent += spentPerMonth[index];
            cumulative[index] = new CumulativeMonth(months[index], income, spent);
        }

        return cumulative;
    }

    static CategorySeries[] ByMonth(IReadOnlyList<YearMonth> months, MoneyEntry[] expenses, CategoryTotal[] ranking)
    {
        var top = ranking.Take(TopCategories).Select(total => total.Category).ToArray();
        var restNeedsOther = ranking.Length > TopCategories && !top.Contains(Other, StringComparer.OrdinalIgnoreCase);
        string[] names = restNeedsOther ? [.. top, Other] : top;

        var topSlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var slot = 0; slot < top.Length; slot++)
        {
            topSlots[top[slot]] = slot;
        }

        var otherSlot = Array.FindIndex(names, name => Same(name, Other));
        var totals = names.Select(_ => new decimal[months.Count]).ToArray();

        foreach (var entry in expenses)
        {
            var slot = topSlots.TryGetValue(entry.Category, out var found) ? found : otherSlot;
            totals[slot][MonthIndex(months[0], entry.Date)] += entry.Money.InPln;
        }

        return [.. names.Select((name, slot) => new CategorySeries(name, totals[slot]))];
    }

    static decimal[] PerMonth(IReadOnlyList<YearMonth> months, IEnumerable<MoneyEntry> entries)
    {
        var totals = new decimal[months.Count];
        foreach (var entry in entries)
        {
            totals[MonthIndex(months[0], entry.Date)] += entry.Money.InPln;
        }

        return totals;
    }

    static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    static int MonthIndex(YearMonth first, DateOnly date) => (date.Year - first.Year) * 12 + date.Month - first.Month;
}
