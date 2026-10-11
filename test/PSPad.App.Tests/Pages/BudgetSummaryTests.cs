using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Dispatch;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetSummaryTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);

    static DateOnly On(int month, int day) => new(2026, month, day);

    IRenderedComponent<BudgetPage> SummaryTab(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "summary"));
        page.WaitForAssertion(() => page.Find(".pspad-summary-range"));
        return page;
    }

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    static string Tile(IRenderedComponent<BudgetPage> page, string name) =>
        Text(page, $".pspad-summary-{name} .pspad-money-tile-value");

    static string TileLabel(IRenderedComponent<BudgetPage> page, string name) =>
        Text(page, $".pspad-summary-{name} .mud-typography-caption");

    static IElement Preset(IRenderedComponent<BudgetPage> page, string text) =>
        page.FindAll(".pspad-summary-presets .mud-toggle-item").Single(item => item.TextContent.Trim() == text);

    [Fact]
    public void ThisYearIsTheDefaultRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = SummaryTab(budget);

        Assert.Equal("1 Jan 2026 – 9 Oct 2026", Text(page, ".pspad-summary-range"));
        Assert.Equal("true", Preset(page, "This year").GetAttribute("aria-checked"));
        Assert.Equal("false", Preset(page, "Last 12 months").GetAttribute("aria-checked"));
        Assert.Equal("false", Preset(page, "Last year").GetAttribute("aria-checked"));
        Assert.Equal("false", Preset(page, "Custom").GetAttribute("aria-checked"));
        Assert.Empty(page.FindAll(".pspad-summary-picker"));
    }

    [Fact]
    public void TheTilesTotalTheRangeInPln()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 1000m, On(3, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 100m, On(3, 2)),
            MoneyEntries.Expense(budget, "Hotel", "Home", 10m, On(3, 3), "EUR", 4.3m),
            MoneyEntries.Expense(budget, "Party", "Food", 999m, new DateOnly(2025, 12, 31)),
            MoneyEntries.Income(budget, "Bonus", "Salary", 5000m, new DateOnly(2025, 6, 1)));

        var page = SummaryTab(budget);

        Assert.Equal("1,000.00 PLN", Tile(page, "income"));
        Assert.Equal("143.00 PLN", Tile(page, "expenses"));
        Assert.Equal("+857.00 PLN", Tile(page, "saved"));
        Assert.Equal("86%", Tile(page, "rate"));
        Assert.Equal("Savings rate", TileLabel(page, "rate"));
    }

    [Fact]
    public void WithoutIncomeTheSavingsRateIsADash()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Bread", "Food", 50m, On(3, 2)));

        var page = SummaryTab(budget);

        Assert.Equal("—", Tile(page, "rate"));
        Assert.Equal("−50.00 PLN", Tile(page, "saved"));
    }

    [Fact]
    public void NetWorthIsTheLatestBalanceOnOrBeforeTheRangeEnd()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, new YearMonth(2026, 8), Balances.AHolding("Wallet", 80m)),
            Balances.Started(budget, new YearMonth(2026, 11), Balances.AHolding("Wallet", 900m)));

        var page = SummaryTab(budget);

        Assert.Equal("Net worth · August 2026", TileLabel(page, "net-worth"));
        Assert.Equal("80.00 PLN", Tile(page, "net-worth"));
    }

    [Fact]
    public void WithoutABalanceNetWorthIsADash()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = SummaryTab(budget);

        Assert.Equal("Net worth", TileLabel(page, "net-worth"));
        Assert.Equal("—", Tile(page, "net-worth"));
    }

    [Fact]
    public void LastYearCoversTheWholePreviousYear()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Party", "Food", 70m, new DateOnly(2025, 12, 31)),
            MoneyEntries.Expense(budget, "Bread", "Food", 5m, On(1, 1)));
        var page = SummaryTab(budget);

        Preset(page, "Last year").Click();

        page.WaitForAssertion(() => Assert.Equal("1 Jan 2025 – 31 Dec 2025", Text(page, ".pspad-summary-range")));
        Assert.Equal("70.00 PLN", Tile(page, "expenses"));
        Assert.Equal("true", Preset(page, "Last year").GetAttribute("aria-checked"));
        Assert.Equal("false", Preset(page, "This year").GetAttribute("aria-checked"));
    }

    [Fact]
    public void ClickingTheSelectedPresetAgainKeepsItsRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = SummaryTab(budget);
        Preset(page, "Last year").Click();
        page.WaitForAssertion(() => Assert.Equal("1 Jan 2025 – 31 Dec 2025", Text(page, ".pspad-summary-range")));

        Preset(page, "Last year").Click();

        Assert.Equal("1 Jan 2025 – 31 Dec 2025", Text(page, ".pspad-summary-range"));
        Assert.Equal("true", Preset(page, "Last year").GetAttribute("aria-checked"));
    }

    [Fact]
    public void SpendingMoreThanEarnedMakesSavedAndTheRateNegative()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 800m, On(3, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 900m, On(3, 2)));

        var page = SummaryTab(budget);

        Assert.Equal("−100.00 PLN", Tile(page, "saved"));
        Assert.Equal("−13%", Tile(page, "rate"));
        Assert.Contains("mud-error-text", page.Find(".pspad-summary-saved .pspad-money-tile-value").ClassList);
        Assert.Contains("mud-error-text", page.Find(".pspad-summary-rate .pspad-money-tile-value").ClassList);
        Assert.DoesNotContain("mud-error-text", page.Find(".pspad-summary-income .pspad-money-tile-value").ClassList);
    }

    [Fact]
    public void TheLastTwelveMonthsStartOnTheFirstOfTheMonthElevenMonthsBack()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Old", "Food", 3m, new DateOnly(2025, 10, 31)),
            MoneyEntries.Expense(budget, "New", "Food", 4m, new DateOnly(2025, 11, 1)));
        var page = SummaryTab(budget);

        Preset(page, "Last 12 months").Click();

        page.WaitForAssertion(() => Assert.Equal("1 Nov 2025 – 9 Oct 2026", Text(page, ".pspad-summary-range")));
        Assert.Equal("4.00 PLN", Tile(page, "expenses"));
    }

    [Fact]
    public async Task ACustomRangeUsesThePickedDays()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "A", "Food", 1m, On(3, 1)),
            MoneyEntries.Expense(budget, "B", "Food", 2m, On(3, 2)),
            MoneyEntries.Expense(budget, "C", "Food", 4m, On(3, 3)),
            MoneyEntries.Expense(budget, "D", "Food", 8m, On(3, 4)));
        var page = SummaryTab(budget);

        Preset(page, "Custom").Click();
        page.WaitForAssertion(() => page.Find(".pspad-summary-picker"));
        Assert.Equal("1 Jan 2026 – 9 Oct 2026", Text(page, ".pspad-summary-range"));

        await page.InvokeAsync(() => page.FindComponent<MudDateRangePicker>().Instance.DateRangeChanged
            .InvokeAsync(new DateRange(new DateTime(2026, 3, 2), new DateTime(2026, 3, 3))));

        page.WaitForAssertion(() => Assert.Equal("2 Mar 2026 – 3 Mar 2026", Text(page, ".pspad-summary-range")));
        Assert.Equal("6.00 PLN", Tile(page, "expenses"));
    }

    [Fact]
    public async Task AHalfPickedRangeKeepsTheCurrentRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "A", "Food", 1m, On(3, 1)),
            MoneyEntries.Expense(budget, "B", "Food", 7m, On(3, 3)));
        var page = SummaryTab(budget);
        Preset(page, "Custom").Click();
        page.WaitForAssertion(() => page.Find(".pspad-summary-picker"));

        await page.InvokeAsync(() => page.FindComponent<MudDateRangePicker>().Instance.DateRangeChanged
            .InvokeAsync(new DateRange(new DateTime(2026, 3, 2), null)));

        Assert.Equal("1 Jan 2026 – 9 Oct 2026", Text(page, ".pspad-summary-range"));
        Assert.Equal("8.00 PLN", Tile(page, "expenses"));
    }

    [Fact]
    public void TheYearFollowsTheUsersTodayNotTheMachineClock()
    {
        var newYear = new DateOnly(2027, 1, 1);
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, new DateOnly(2026, 12, 31), budget,
            MoneyEntries.Expense(budget, "Party", "Food", 999m, new DateOnly(2026, 12, 31)),
            MoneyEntries.Expense(budget, "Bread", "Food", 5m, newYear));
        Services.GetRequiredService<AppState>().Today = newYear;

        var page = SummaryTab(budget);

        Assert.Equal("1 Jan 2027 – 1 Jan 2027", Text(page, ".pspad-summary-range"));
        Assert.Equal("5.00 PLN", Tile(page, "expenses"));
    }

    [Fact]
    public async Task ANewEntryShowsUpInTheSummary()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = SummaryTab(budget);
        Assert.Equal("0.00 PLN", Tile(page, "expenses"));

        await page.InvokeAsync(() => Services.GetRequiredService<CommandSender>().SendAsync(
            new RecordExpense(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, "Bread", "Food",
                new Money(12m, "PLN", 1m, Today), Today, null)));

        page.WaitForAssertion(() => Assert.Equal("12.00 PLN", Tile(page, "expenses")));
    }

    [Fact]
    public void TheSummaryTabHasNoFab()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = SummaryTab(budget);

        Assert.Empty(page.FindAll(".pspad-fab"));
    }

    [Fact]
    public void AnArchivedBudgetStillShowsItsSummary()
    {
        var budget = BudgetsPageTests.Named("Old");
        var pay = MoneyEntries.Income(budget, "Pay", "Salary", 300m, On(2, 1));
        Balances.Archived(budget);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, pay);

        var page = SummaryTab(budget);

        Assert.Equal("300.00 PLN", Tile(page, "income"));
        Assert.Contains("This budget is archived", page.Markup);
        Assert.Empty(page.FindAll(".pspad-fab"));
    }
}
