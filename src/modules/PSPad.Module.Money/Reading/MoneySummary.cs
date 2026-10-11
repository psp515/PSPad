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
        var cumulative = new CumulativeMonth[months.Count];
        var income = 0m;
        var spent = 0m;

        for (var index = 0; index < months.Count; index++)
        {
            income += PlnIn(incomes, months[index]);
            spent += PlnIn(expenses, months[index]);
            cumulative[index] = new CumulativeMonth(months[index], income, spent);
        }

        return cumulative;
    }

    static CategorySeries[] ByMonth(IReadOnlyList<YearMonth> months, MoneyEntry[] expenses, CategoryTotal[] ranking)
    {
        var top = ranking.Take(TopCategories).Select(total => total.Category).ToArray();
        var restNeedsOther = ranking.Length > TopCategories && !top.Contains(Other, StringComparer.OrdinalIgnoreCase);
        string[] names = restNeedsOther ? [.. top, Other] : top;

        return
        [
            .. names.Select(name => new CategorySeries(name,
            [
                .. months.Select(month => PlnIn(expenses.Where(entry => Same(SeriesOf(entry.Category, top), name)), month))
            ]))
        ];
    }

    static string SeriesOf(string category, string[] top) =>
        top.FirstOrDefault(name => Same(name, category)) ?? Other;

    static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    static decimal PlnIn(IEnumerable<MoneyEntry> entries, YearMonth month) =>
        entries.Where(entry => month.Contains(entry.Date)).Sum(entry => entry.Money.InPln);
}
