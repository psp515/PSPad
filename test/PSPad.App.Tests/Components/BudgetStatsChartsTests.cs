using Bunit;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.Tests.Pages;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class BudgetStatsChartsTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);
    static readonly DateOnly From = new(2026, 1, 1);
    static readonly DateOnly To = new(2026, 3, 31);
    static readonly YearMonth Next = new(2026, 4);

    static DateOnly On(int month, int day) => new(2026, month, day);

    IRenderedComponent<BudgetStatsCharts> Charts(MoneyEntry[] entries, BalanceSnapshot[] snapshots)
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Today);
        var summary = MoneySummary.For(entries, snapshots, From, To);
        return Render<BudgetStatsCharts>(parameters => parameters
            .Add(p => p.Summary, summary)
            .Add(p => p.Usual, UsualMonth.For(entries, Next))
            .Add(p => p.Next, Next));
    }

    static IRenderedComponent<MudChart<double>> ChartWith(IRenderedComponent<BudgetStatsCharts> charts, string series) =>
        charts.FindComponents<MudChart<double>>().Single(chart => chart.Instance.ChartSeries.Any(s => s.Name == series));

    static double[] Data(IRenderedComponent<MudChart<double>> chart, string series) =>
        [.. chart.Instance.ChartSeries.Single(s => s.Name == series).Data.Values];

    static string[] Headers(IRenderedComponent<BudgetStatsCharts> charts, string card) =>
        [.. charts.FindAll($".{card} table.mud-sr-only thead th").Select(cell => cell.TextContent.Trim())];

    static List<string[]> Rows(IRenderedComponent<BudgetStatsCharts> charts, string card) =>
    [
        .. charts.FindAll($".{card} table.mud-sr-only tbody tr")
            .Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToArray())
    ];

    static MoneyEntry[] IncomeAndSpending(Budget budget) =>
    [
        MoneyEntries.Income(budget, "Pay", "Salary", 1000m, On(1, 5)),
        MoneyEntries.Expense(budget, "Bread", "Food", 200m, On(1, 6)),
        MoneyEntries.Income(budget, "Pay", "Salary", 500m, On(3, 1)),
        MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(3, 31))
    ];

    [Fact]
    public void TheMonthlyChartBarsIncomeAgainstExpenses()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        var chart = ChartWith(charts, "Income");
        Assert.Equal(ChartType.Bar, chart.Instance.ChartType);
        Assert.Equal([1000d, 0d, 500d], Data(chart, "Income"));
        Assert.Equal([200d, 0d, 300d], Data(chart, "Expenses"));
        Assert.Equal(["Jan", "Feb", "Mar"], chart.Instance.ChartLabels);
        Assert.Contains("Income vs expenses by month", charts.Find(".pspad-stats-by-month").TextContent);
    }

    [Fact]
    public void TheMonthlyTableCarriesEveryMonthForScreenReaders()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        Assert.Equal(["Month", "Income", "Expenses", "Net"], Headers(charts, "pspad-stats-by-month"));
        var rows = Rows(charts, "pspad-stats-by-month");
        Assert.Equal(["January 2026", "1,000.00 PLN", "200.00 PLN", "+800.00 PLN"], rows[0]);
        Assert.Equal(["February 2026", "0.00 PLN", "0.00 PLN", "0.00 PLN"], rows[1]);
        Assert.Equal(["March 2026", "500.00 PLN", "300.00 PLN", "+200.00 PLN"], rows[2]);
    }

    [Fact]
    public void TheMonthlyYAxisScalesToTheLargestMonth()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        var options = Assert.IsType<BarChartOptions>(ChartWith(charts, "Income").Instance.ChartOptions);
        Assert.Equal(200, options.YAxisTicks);
    }

    [Fact]
    public void MonthlyExpensesStackTheTopFourCategoriesAndOther()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var charts = Charts(
        [
            MoneyEntries.Expense(budget, "Rent", "Home", 500m, On(1, 3)),
            MoneyEntries.Expense(budget, "Bread", "Food", 400m, On(2, 3)),
            MoneyEntries.Expense(budget, "Fuel", "Car", 300m, On(1, 4)),
            MoneyEntries.Expense(budget, "Mouse", "PC", 200m, On(2, 4)),
            MoneyEntries.Expense(budget, "Tyre", "Bike", 50m, On(1, 5))
        ], []);

        var chart = ChartWith(charts, "Other");
        Assert.Equal(ChartType.StackedBar, chart.Instance.ChartType);
        Assert.Equal(["Home", "Food", "Car", "PC", "Other"], chart.Instance.ChartSeries.Select(series => series.Name));
        Assert.Equal([500d, 0d, 0d], Data(chart, "Home"));
        Assert.Equal([50d, 0d, 0d], Data(chart, "Other"));
        Assert.Equal(200, Assert.IsType<StackedBarChartOptions>(chart.Instance.ChartOptions).YAxisTicks);
        Assert.Contains("Spend by category per month", charts.Find(".pspad-stats-categories").TextContent);
        Assert.Equal(["Month", "Home", "Food", "Car", "PC", "Other"], Headers(charts, "pspad-stats-categories"));
        var rows = Rows(charts, "pspad-stats-categories");
        Assert.Equal(3, rows.Count);
        Assert.Equal(["January 2026", "500.00 PLN", "0.00 PLN", "300.00 PLN", "0.00 PLN", "50.00 PLN"], rows[0]);
        Assert.Equal(["February 2026", "0.00 PLN", "400.00 PLN", "0.00 PLN", "200.00 PLN", "0.00 PLN"], rows[1]);
    }

    [Fact]
    public void NetWorthStacksHoldingTypesAndLeavesAGapForAMonthWithoutABalance()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var charts = Charts([],
        [
            Balances.Started(budget, new YearMonth(2026, 1),
                Balances.AHolding("Wallet", 100m),
                Balances.AHolding("Konto", 10m, "EUR", 4.30m, HoldingType.Savings)),
            Balances.Started(budget, new YearMonth(2026, 3),
                Balances.AHolding("Broker", 5m, "EUR", 4.20m, HoldingType.Investment))
        ]);

        var chart = ChartWith(charts, "Cash");
        Assert.Equal(ChartType.StackedBar, chart.Instance.ChartType);
        Assert.Equal(["Cash", "Savings", "Investment"], chart.Instance.ChartSeries.Select(series => series.Name));
        Assert.Equal([100d, 0d, 0d], Data(chart, "Cash"));
        Assert.Equal([43d, 0d, 0d], Data(chart, "Savings"));
        Assert.Equal([0d, 0d, 21d], Data(chart, "Investment"));
        Assert.Equal(50, Assert.IsType<StackedBarChartOptions>(chart.Instance.ChartOptions).YAxisTicks);
        Assert.Equal(["Month", "Cash", "Savings", "Investment", "Total"], Headers(charts, "pspad-stats-net-worth-chart"));
        var rows = Rows(charts, "pspad-stats-net-worth-chart");
        Assert.Equal(3, rows.Count);
        Assert.Equal(["January 2026", "100.00 PLN", "43.00 PLN", "0.00 PLN", "143.00 PLN"], rows[0]);
        Assert.Equal(["February 2026", "—", "—", "—", "—"], rows[1]);
        Assert.Equal(["March 2026", "0.00 PLN", "0.00 PLN", "21.00 PLN", "21.00 PLN"], rows[2]);
    }

    [Fact]
    public void TheRankingListsEveryCategoryLargestFirst()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var charts = Charts(
        [
            MoneyEntries.Expense(budget, "Tyre", "Bike", 50m, On(1, 5)),
            MoneyEntries.Expense(budget, "Rent", "Home", 500m, On(1, 3)),
            MoneyEntries.Expense(budget, "Bread", "Food", 400m, On(2, 3))
        ], []);

        Assert.Contains("Where money goes", charts.Find(".pspad-stats-ranking").TextContent);
        Assert.Equal(["Home", "Food", "Bike"],
            charts.FindAll(".pspad-stats-ranking .pspad-category-open").Select(name => name.TextContent.Trim()));
        Assert.Equal(["500.00 PLN", "400.00 PLN", "50.00 PLN"],
            charts.FindAll(".pspad-stats-ranking-pln").Select(pln => pln.TextContent.Trim()));
        var bars = charts.FindAll(".pspad-stats-ranking [role='progressbar']");
        Assert.Equal(["500", "400", "50"], bars.Select(bar => bar.GetAttribute("aria-valuenow")));
        Assert.Equal(["500", "500", "500"], bars.Select(bar => bar.GetAttribute("aria-valuemax")));
        Assert.Equal(["true", "true", "true"], bars.Select(bar => bar.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void WithoutExpensesTheCategoryCardsSaySo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var charts = Charts([MoneyEntries.Income(budget, "Pay", "Salary", 1000m, On(1, 5))], []);

        Assert.Equal("No expenses in this range.", charts.Find(".pspad-stats-no-expenses").TextContent.Trim());
        Assert.Equal("No expenses in this range.", charts.Find(".pspad-stats-no-ranking").TextContent.Trim());
        Assert.Empty(charts.FindAll(".pspad-stats-ranking-row"));
    }

    [Fact]
    public void WithoutBalancesTheNetWorthCardSaysSo()
    {
        var charts = Charts([], []);

        Assert.Equal("No balances in this range.", charts.Find(".pspad-stats-no-balances").TextContent.Trim());
        Assert.Single(charts.FindComponents<MudChart<double>>());
        Assert.Empty(charts.FindAll(".pspad-stats-net-worth-chart table"));
    }

    [Fact]
    public void AnEmptyRangeStillDrawsTheMonthlyChartAtZero()
    {
        var charts = Charts([], []);

        var chart = ChartWith(charts, "Income");
        Assert.Equal([0d, 0d, 0d], Data(chart, "Income"));
        Assert.Equal(1, Assert.IsType<BarChartOptions>(chart.Instance.ChartOptions).YAxisTicks);
    }
}
