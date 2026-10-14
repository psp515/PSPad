using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using PSPad.App.Pages;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetOverviewTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 10);

    static DateOnly On(int month, int day) => new(2026, month, day);

    IRenderedComponent<BudgetPage> Overview(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-overview"));
        return page;
    }

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    [Fact]
    public void TheHeroComparesTheMonthSoFarWithTheUsualMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 3100m, On(9, 28)),
            MoneyEntries.Expense(budget, "Rent", "Home", 620m, On(9, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(10, 1)),
            MoneyEntries.Income(budget, "Pay", "Salary", 500m, On(10, 2)));

        var page = Overview(budget);

        Assert.Equal("October 2026 · day 10 of 31", Text(page, ".pspad-overview-day"));
        Assert.Equal("+200.00 PLN", Text(page, ".pspad-overview-net .pspad-money-value"));
        Assert.Equal("500.00 PLN", Text(page, ".pspad-overview-income .pspad-money-value"));
        Assert.Equal("usual by now 1,000.00 PLN", Text(page, ".pspad-overview-income-usual"));
        Assert.Equal("300.00 PLN", Text(page, ".pspad-overview-expenses .pspad-money-value"));
        Assert.Equal("+100.00 PLN vs usual by now", Text(page, ".pspad-overview-expenses-delta"));
        Assert.Equal("≈ 620.00 PLN", Text(page, ".pspad-overview-expected .pspad-money-value"));
        Assert.Equal("avg of last 1 months", Text(page, ".pspad-overview-expected-caption"));
        Assert.Equal("Spent 48% of a usual month · 32% of the month passed", Text(page, ".pspad-overview-pace-caption"));
        Assert.Single(page.FindAll(".pspad-category-legend"));
    }

    [Fact]
    public void WithoutABaselineTheHeroSaysAveragesComeLater()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(10, 1)));

        var page = Overview(budget);

        Assert.Equal("Averages appear after a full month of entries.", Text(page, ".pspad-overview-no-usual"));
        Assert.Empty(page.FindAll(".pspad-overview-income-usual"));
        Assert.Empty(page.FindAll(".pspad-overview-expected"));
        Assert.Empty(page.FindAll(".pspad-overview-pace"));
    }

    static MoneyEntry[] Baseline(Budget budget) =>
    [
        MoneyEntries.Income(budget, "Pay", "Salary", 3000m, On(8, 28)),
        MoneyEntries.Income(budget, "Pay", "Salary", 3000m, On(9, 28)),
        MoneyEntries.Expense(budget, "Groceries", "Food", 620m, On(9, 3)),
        MoneyEntries.Expense(budget, "Rent", "Home", 2000m, On(9, 1))
    ];

    [Fact]
    public void InsightsListWhatIsWorthALookAndOpenTheirCategory()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today,
            [budget, .. Baseline(budget), MoneyEntries.Expense(budget, "Groceries", "Food", 700m, On(10, 2))]);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}");
        var page = Overview(budget);

        var insights = page.FindAll(".pspad-insight");
        Assert.Equal(["Food already past a usual month", "Salary usually lands around the 28th"],
            insights.Select(insight => insight.QuerySelector(".pspad-insight-title")!.TextContent.Trim()));
        Assert.Equal(["past-usual", "income-expected"], insights.Select(insight => insight.GetAttribute("data-kind")));

        insights[0].Click();

        Assert.EndsWith($"/budgets/{budget.Id}/entries", navigation.Uri);
        page.Render(parameters => parameters.Add(p => p.Tab, "entries"));

        page.WaitForAssertion(() => Assert.Equal("Food", Text(page, ".pspad-entries-category")));
    }

    [Fact]
    public void WithNothingUnusualTheInsightsSaySo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today,
            [budget, .. Baseline(budget).Where(entry => entry.Kind == CategoryKind.Expense)]);

        var page = Overview(budget);

        Assert.Equal("Nothing unusual so far.", Text(page, ".pspad-overview-quiet"));
    }

    [Fact]
    public void WhereMoneyWentListsCategoriesWithTheirDeltaAndOpensThem()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today,
            [budget, .. Baseline(budget), MoneyEntries.Expense(budget, "Groceries", "Food", 300m, On(10, 2)), MoneyEntries.Expense(budget, "Bus", "Car", 20m, On(10, 3))]);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}");
        var page = Overview(budget);

        var rows = page.FindAll(".pspad-category-pace");
        Assert.Equal(["Food", "Car"], rows.Select(row => row.QuerySelector(".pspad-category-open")!.TextContent.Trim()));
        Assert.Equal(["300.00 PLN", "20.00 PLN"], rows.Select(row => row.QuerySelector(".pspad-category-spent")!.TextContent.Trim()));
        Assert.Equal("+200.00 PLN", rows[0].QuerySelector(".pspad-category-delta")!.TextContent.Trim());
        Assert.Null(rows[1].QuerySelector(".pspad-category-delta"));

        rows[1].QuerySelector(".pspad-category-open")!.Click();

        Assert.EndsWith($"/budgets/{budget.Id}/entries", navigation.Uri);
        page.Render(parameters => parameters.Add(p => p.Tab, "entries"));

        page.WaitForAssertion(() => Assert.Equal("Car", Text(page, ".pspad-entries-category")));
    }

    [Fact]
    public void RecentEntriesAreTheLastFiveRecordedWithTheirTime()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var entries = Enumerable.Range(1, 6)
            .Select(index => MoneyEntries.Expense(budget, $"Entry {index}", "Food", index, On(9, index),
                at: new DateTimeOffset(2026, 10, 9, 10 + index, 0, 0, TimeSpan.Zero)))
            .ToArray();
        AppTestHost.Arrange(this, budget.UserId, Today, [budget, .. entries]);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}");
        var page = Overview(budget);

        var rows = page.FindAll(".pspad-overview-recent .pspad-entry-row");
        Assert.Equal(["Entry 6", "Entry 5", "Entry 4", "Entry 3", "Entry 2"],
            rows.Select(row => row.QuerySelector(".pspad-entry-name")!.TextContent.Trim()));
        Assert.Equal("recorded yesterday 16:00", rows[0].QuerySelector(".pspad-entry-recorded")!.TextContent.Trim());

        page.Find(".pspad-overview-all-entries").Click();

        Assert.EndsWith($"/budgets/{budget.Id}/entries", navigation.Uri);
    }

    [Fact]
    public async Task TheNetWorthCardShowsTheLatestBalanceAndOffersToStartTheMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, new YearMonth(2026, 8), Balances.AHolding("Wallet", 80m)),
            Balances.Started(budget, new YearMonth(2026, 9), Balances.AHolding("Wallet", 100m)));
        var page = Overview(budget);

        Assert.Equal("Net worth · September 2026", Text(page, ".pspad-overview-net-worth .pspad-overview-net-worth-label"));
        Assert.Equal("100.00 PLN", Text(page, ".pspad-overview-net-worth .pspad-money-value"));
        Assert.Equal("+20.00 PLN vs August 2026", Text(page, ".pspad-overview-net-worth-change"));
        Assert.Equal("October 2026 has no balance yet", Text(page, ".pspad-overview-no-balance"));

        page.Find(".pspad-overview-start-month").Click();

        var sent = await AppTestHost.SentAsync<StartBalanceMonth>(this);
        Assert.Equal(new YearMonth(2026, 10), sent.Month);
    }

    [Fact]
    public void WithoutAnyBalanceTheCardIsADash()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Overview(budget);

        Assert.Equal("—", Text(page, ".pspad-overview-net-worth .pspad-money-value"));
        Assert.Equal("No balance yet", Text(page, ".pspad-overview-no-balance"));
        Assert.Empty(page.FindAll(".pspad-overview-start-month"));
    }
}
