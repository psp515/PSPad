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
public class BudgetSummaryChartsTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);
    static readonly DateOnly From = new(2026, 1, 1);
    static readonly DateOnly To = new(2026, 3, 31);

    static DateOnly On(int month, int day) => new(2026, month, day);

    IRenderedComponent<BudgetSummaryCharts> Charts(MoneyEntry[] entries, BalanceSnapshot[] snapshots)
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Today);
        var summary = MoneySummary.For(entries, snapshots, From, To);
        return Render<BudgetSummaryCharts>(parameters => parameters.Add(p => p.Summary, summary));
    }

    static IRenderedComponent<MudChart<double>> ChartWith(IRenderedComponent<BudgetSummaryCharts> charts, string series) =>
        charts.FindComponents<MudChart<double>>().Single(chart => chart.Instance.ChartSeries.Any(s => s.Name == series));

    static double[] Data(IRenderedComponent<MudChart<double>> chart, string series) =>
        [.. chart.Instance.ChartSeries.Single(s => s.Name == series).Data.Values];

    static string[] Headers(IRenderedComponent<BudgetSummaryCharts> charts, string card) =>
        [.. charts.FindAll($".{card} table.mud-sr-only thead th").Select(cell => cell.TextContent.Trim())];

    static List<string[]> Rows(IRenderedComponent<BudgetSummaryCharts> charts, string card) =>
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
    public void TheCumulativeChartRunsIncomeAndExpensesMonthByMonth()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        var chart = ChartWith(charts, "Income");
        Assert.Equal(ChartType.Line, chart.Instance.ChartType);
        Assert.Equal([1000d, 1000d, 1500d], Data(chart, "Income"));
        Assert.Equal([200d, 200d, 500d], Data(chart, "Expenses"));
        Assert.Equal(["Jan", "Feb", "Mar"], chart.Instance.ChartLabels);
        Assert.Contains("Income vs expenses", charts.Find(".pspad-summary-cumulative").TextContent);
    }

    [Fact]
    public void TheCumulativeTableCarriesEveryMonthForScreenReaders()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        Assert.Equal(["Month", "Income", "Expenses"], Headers(charts, "pspad-summary-cumulative"));
        var rows = Rows(charts, "pspad-summary-cumulative");
        Assert.Equal(3, rows.Count);
        Assert.Equal(["January 2026", "1,000.00 PLN", "200.00 PLN"], rows[0]);
        Assert.Equal(["February 2026", "1,000.00 PLN", "200.00 PLN"], rows[1]);
        Assert.Equal(["March 2026", "1,500.00 PLN", "500.00 PLN"], rows[2]);
    }

    [Fact]
    public void TheCumulativeYAxisScalesToTheLargestRunningTotal()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        var options = Assert.IsType<LineChartOptions>(ChartWith(charts, "Income").Instance.ChartOptions);
        Assert.Equal(500, options.YAxisTicks);
    }

    [Fact]
    public void TheCumulativeChartStartsAtZeroAndMarksEachMonth()
    {
        var charts = Charts(IncomeAndSpending(BudgetsPageTests.Named("Personal")), []);

        var options = Assert.IsType<LineChartOptions>(ChartWith(charts, "Income").Instance.ChartOptions);
        Assert.True(options.YAxisRequireZeroPoint);
        Assert.True(options.ShowDataMarkers);
    }

    [Fact]
    public void AOneMonthRangeStillDrawsVisibleMarkersFromZero()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Today);
        var budget = BudgetsPageTests.Named("Personal");
        var summary = MoneySummary.For(
            [MoneyEntries.Income(budget, "Pay", "Salary", 5000m, On(1, 5)), MoneyEntries.Expense(budget, "Rent", "Home", 3000m, On(1, 6))],
            [], From, new DateOnly(2026, 1, 31));

        var charts = Render<BudgetSummaryCharts>(parameters => parameters.Add(p => p.Summary, summary));

        var line = charts.Find(".pspad-summary-cumulative svg");
        Assert.Equal(2, line.QuerySelectorAll("circle[r='3.5']").Length);
        Assert.Contains(">0<", line.InnerHtml);
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
        Assert.Equal(["Month", "Home", "Food", "Car", "PC", "Other"], Headers(charts, "pspad-summary-by-month"));
        var rows = Rows(charts, "pspad-summary-by-month");
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
        Assert.Equal(["Month", "Cash", "Savings", "Investment", "Total"], Headers(charts, "pspad-summary-net-worth-chart"));
        var rows = Rows(charts, "pspad-summary-net-worth-chart");
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

        Assert.Equal(["Home", "Food", "Bike"],
            charts.FindAll(".pspad-summary-ranking-name").Select(name => name.TextContent.Trim()));
        Assert.Equal(["500.00 PLN", "400.00 PLN", "50.00 PLN"],
            charts.FindAll(".pspad-summary-ranking-pln").Select(pln => pln.TextContent.Trim()));
        var bars = charts.FindAll(".pspad-summary-ranking [role='progressbar']");
        Assert.Equal(["500", "400", "50"], bars.Select(bar => bar.GetAttribute("aria-valuenow")));
        Assert.Equal(["500", "500", "500"], bars.Select(bar => bar.GetAttribute("aria-valuemax")));
        Assert.Equal(["true", "true", "true"], bars.Select(bar => bar.GetAttribute("aria-hidden")));
    }

    [Fact]
    public void WithoutExpensesTheCategoryCardsSaySo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var charts = Charts([MoneyEntries.Income(budget, "Pay", "Salary", 1000m, On(1, 5))], []);

        Assert.Equal("No expenses in this range.", charts.Find(".pspad-summary-no-expenses").TextContent.Trim());
        Assert.Equal("No expenses in this range.", charts.Find(".pspad-summary-no-ranking").TextContent.Trim());
        Assert.Empty(charts.FindAll(".pspad-summary-ranking-row"));
    }

    [Fact]
    public void WithoutBalancesTheNetWorthCardSaysSo()
    {
        var charts = Charts([], []);

        Assert.Equal("No balances in this range.", charts.Find(".pspad-summary-no-balances").TextContent.Trim());
        Assert.Single(charts.FindComponents<MudChart<double>>());
        Assert.Empty(charts.FindAll(".pspad-summary-net-worth-chart table"));
    }

    [Fact]
    public void AnEmptyRangeStillDrawsTheCumulativeChartAtZero()
    {
        var charts = Charts([], []);

        var chart = ChartWith(charts, "Income");
        Assert.Equal([0d, 0d, 0d], Data(chart, "Income"));
        Assert.Equal(1, Assert.IsType<LineChartOptions>(chart.Instance.ChartOptions).YAxisTicks);
    }
}
