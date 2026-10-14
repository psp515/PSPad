using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
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
public class BudgetStatsTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);

    static DateOnly On(int month, int day) => new(2026, month, day);

    IRenderedComponent<BudgetPage> StatsTab(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "stats"));
        page.WaitForAssertion(() => page.Find(".pspad-stats-range"));
        return page;
    }

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    static string Tile(IRenderedComponent<BudgetPage> page, string name) =>
        Text(page, $".pspad-stats-{name} .pspad-money-value");

    static IElement Preset(IRenderedComponent<BudgetPage> page, string text) =>
        page.FindAll(".pspad-stats-presets .mud-chip").Single(item => item.TextContent.Trim() == text);

    [Fact]
    public void TheLastSixMonthsAreTheDefaultRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, MoneyEntries.Expense(budget, "Bread", "Food", 50m, On(9, 2)));

        var page = StatsTab(budget);

        Assert.Equal("1 Apr 2026 – 30 Sep 2026 · 1 month with entries", Text(page, ".pspad-stats-range"));
        Assert.Equal("true", Preset(page, "Last 6 months").GetAttribute("aria-pressed"));
        Assert.Equal("false", Preset(page, "This year").GetAttribute("aria-pressed"));
        Assert.Equal("false", Preset(page, "Last 12 months").GetAttribute("aria-pressed"));
        Assert.Equal("false", Preset(page, "Last year").GetAttribute("aria-pressed"));
        Assert.Equal("false", Preset(page, "Custom").GetAttribute("aria-pressed"));
        Assert.Empty(page.FindAll(".pspad-stats-picker"));
    }

    [Fact]
    public void TheKpisAverageOverMonthsWithEntries()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 1000m, On(8, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 400m, On(8, 2)),
            MoneyEntries.Income(budget, "Pay", "Salary", 3000m, On(9, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 600m, On(9, 2)));

        var page = StatsTab(budget);

        Assert.Equal("2,000.00 PLN", Tile(page, "avg-income"));
        Assert.Equal("lowest 1,000.00 · highest 3,000.00", Text(page, ".pspad-stats-avg-income .pspad-stats-kpi-caption"));
        Assert.Equal("500.00 PLN", Tile(page, "avg-expenses"));
        Assert.Equal("+1,500.00 PLN", Tile(page, "avg-saved"));
        Assert.Equal("saved in 2 of 2 months", Text(page, ".pspad-stats-avg-saved .pspad-stats-kpi-caption"));
        Assert.Equal("75%", Tile(page, "rate"));
    }

    [Fact]
    public void WithoutEntriesTheKpisAreDashes()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = StatsTab(budget);

        Assert.Equal("—", Tile(page, "avg-income"));
        Assert.Equal("—", Tile(page, "avg-saved"));
        Assert.Equal("—", Tile(page, "rate"));
    }

    [Fact]
    public void TheForecastIsTheUsualMonthWhateverThePreset()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 3000m, On(9, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 600m, On(9, 2)),
            MoneyEntries.Expense(budget, "Bread", "Food", 100m, On(9, 3)),
            MoneyEntries.Expense(budget, "Party", "Food", 9999m, new DateOnly(2025, 12, 31)));
        var page = StatsTab(budget);

        Preset(page, "Last 12 months").Click();

        Assert.Contains("November 2026 forecast", Text(page, ".pspad-stats-forecast"));
        Assert.Equal("≈ 700.00 PLN", Text(page, ".pspad-stats-forecast-expenses .pspad-money-value"));
        Assert.Equal("≈ 3,000.00 PLN", Text(page, ".pspad-stats-forecast-income .pspad-money-value"));
        Assert.Equal("≈ +2,300.00 PLN", Text(page, ".pspad-stats-forecast-saved .pspad-money-value"));
        Assert.Equal(["Home", "Food"], page.FindAll(".pspad-stats-forecast .pspad-category-open").Select(name => name.TextContent.Trim()));
        Assert.Equal(["600.00 PLN", "100.00 PLN"], page.FindAll(".pspad-stats-forecast .pspad-stats-forecast-amount").Select(amount => amount.TextContent.Trim()));
    }

    [Fact]
    public void WithoutAFullMonthThereIsNoForecast()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, MoneyEntries.Expense(budget, "Bread", "Food", 100m, On(10, 3)));

        var page = StatsTab(budget);

        Assert.Equal("A forecast needs at least one full month of entries.", Text(page, ".pspad-stats-forecast-empty"));
    }

    [Fact]
    public void TheRankingShowsEachCategorysShareAndOpensIt()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(9, 2)),
            MoneyEntries.Expense(budget, "Bread", "Food", 100m, On(9, 3)));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}/stats");
        var page = StatsTab(budget);

        Assert.Equal(["75%", "25%"], page.FindAll(".pspad-stats-ranking-share").Select(share => share.TextContent.Trim()));

        page.FindAll(".pspad-stats-ranking .pspad-category-open").Single(name => name.TextContent.Trim() == "Food").Click();

        Assert.EndsWith($"/budgets/{budget.Id}/entries", navigation.Uri);
        page.Render(parameters => parameters.Add(p => p.Tab, "entries"));
        page.WaitForAssertion(() => Assert.Equal("Food", Text(page, ".pspad-entries-category")));
    }

    [Fact]
    public void AForecastCategoryOpensItsEntries()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(9, 2)));
        var page = StatsTab(budget);

        page.FindAll(".pspad-stats-forecast .pspad-category-open").Single().Click();

        page.Render(parameters => parameters.Add(p => p.Tab, "entries"));
        page.WaitForAssertion(() => Assert.Equal("Home", Text(page, ".pspad-entries-category")));
    }

    [Fact]
    public void LastYearCoversTheWholePreviousYear()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Party", "Food", 70m, new DateOnly(2025, 12, 31)),
            MoneyEntries.Expense(budget, "Bread", "Food", 5m, On(1, 1)));
        var page = StatsTab(budget);

        Preset(page, "Last year").Click();

        page.WaitForAssertion(() => Assert.Equal("1 Jan 2025 – 31 Dec 2025 · 1 month with entries", Text(page, ".pspad-stats-range")));
        Assert.Equal("70.00 PLN", Tile(page, "avg-expenses"));
        Assert.Equal("true", Preset(page, "Last year").GetAttribute("aria-pressed"));
        Assert.Equal("false", Preset(page, "Last 6 months").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void ClickingTheSelectedPresetAgainKeepsItsRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = StatsTab(budget);
        Preset(page, "Last year").Click();
        page.WaitForAssertion(() => Assert.Equal("1 Jan 2025 – 31 Dec 2025 · 0 months with entries", Text(page, ".pspad-stats-range")));

        Preset(page, "Last year").Click();

        Assert.Equal("1 Jan 2025 – 31 Dec 2025 · 0 months with entries", Text(page, ".pspad-stats-range"));
        Assert.Equal("true", Preset(page, "Last year").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void SpendingMoreThanEarnedMakesSavedAndTheRateNegative()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 800m, On(9, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 900m, On(9, 2)));

        var page = StatsTab(budget);

        Assert.Equal("−100.00 PLN", Tile(page, "avg-saved"));
        Assert.Equal("−13%", Tile(page, "rate"));
        Assert.Equal("saved in 0 of 1 month", Text(page, ".pspad-stats-avg-saved .pspad-stats-kpi-caption"));
        Assert.Contains("mud-error-text", page.Find(".pspad-stats-avg-saved .pspad-money-value").ClassList);
        Assert.Contains("mud-error-text", page.Find(".pspad-stats-rate .pspad-money-value").ClassList);
        Assert.DoesNotContain("mud-error-text", page.Find(".pspad-stats-avg-income .pspad-money-value").ClassList);
    }

    [Fact]
    public void ARateThatRoundsToZeroIsNotRed()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 1000m, On(9, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 1004m, On(9, 2)));

        var page = StatsTab(budget);

        Assert.Equal("0%", Tile(page, "rate"));
        Assert.DoesNotContain("mud-error-text", page.Find(".pspad-stats-rate .pspad-money-value").ClassList);
        Assert.Contains("mud-error-text", page.Find(".pspad-stats-avg-saved .pspad-money-value").ClassList);
    }

    [Fact]
    public void TheLastTwelveMonthsStartOnTheFirstOfTheMonthElevenMonthsBack()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Old", "Food", 3m, new DateOnly(2025, 10, 31)),
            MoneyEntries.Expense(budget, "New", "Food", 4m, new DateOnly(2025, 11, 1)));
        var page = StatsTab(budget);

        Preset(page, "Last 12 months").Click();

        page.WaitForAssertion(() => Assert.Equal("1 Nov 2025 – 9 Oct 2026 · 1 month with entries", Text(page, ".pspad-stats-range")));
        Assert.Equal("4.00 PLN", Tile(page, "avg-expenses"));
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
        var page = StatsTab(budget);

        Preset(page, "Custom").Click();
        page.WaitForAssertion(() => page.Find(".pspad-stats-picker"));
        Assert.Equal("1 Apr 2026 – 30 Sep 2026 · 0 months with entries", Text(page, ".pspad-stats-range"));

        await page.InvokeAsync(() => page.FindComponent<MudDateRangePicker>().Instance.DateRangeChanged
            .InvokeAsync(new DateRange(new DateTime(2026, 3, 2), new DateTime(2026, 3, 3))));

        page.WaitForAssertion(() => Assert.Equal("2 Mar 2026 – 3 Mar 2026 · 1 month with entries", Text(page, ".pspad-stats-range")));
        Assert.Equal("6.00 PLN", Tile(page, "avg-expenses"));
    }

    [Fact]
    public async Task AHalfPickedRangeKeepsTheCurrentRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "A", "Food", 1m, On(9, 1)),
            MoneyEntries.Expense(budget, "B", "Food", 7m, On(9, 3)));
        var page = StatsTab(budget);
        Preset(page, "Custom").Click();
        page.WaitForAssertion(() => page.Find(".pspad-stats-picker"));

        await page.InvokeAsync(() => page.FindComponent<MudDateRangePicker>().Instance.DateRangeChanged
            .InvokeAsync(new DateRange(new DateTime(2026, 9, 2), null)));

        Assert.Equal("1 Apr 2026 – 30 Sep 2026 · 1 month with entries", Text(page, ".pspad-stats-range"));
        Assert.Equal("8.00 PLN", Tile(page, "avg-expenses"));
    }

    [Fact]
    public void TheRangeFollowsTheUsersTodayNotTheMachineClock()
    {
        var newYear = new DateOnly(2027, 1, 1);
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, new DateOnly(2026, 12, 31), budget,
            MoneyEntries.Expense(budget, "Party", "Food", 999m, new DateOnly(2026, 12, 31)),
            MoneyEntries.Expense(budget, "Bread", "Food", 5m, newYear));
        Services.GetRequiredService<AppState>().Today = newYear;
        var page = StatsTab(budget);
        Assert.Equal("1 Jul 2026 – 31 Dec 2026 · 1 month with entries", Text(page, ".pspad-stats-range"));
        Assert.Equal("999.00 PLN", Tile(page, "avg-expenses"));

        Preset(page, "This year").Click();

        page.WaitForAssertion(() => Assert.Equal("1 Jan 2027 – 1 Jan 2027 · 1 month with entries", Text(page, ".pspad-stats-range")));
        Assert.Equal("5.00 PLN", Tile(page, "avg-expenses"));
    }

    [Fact]
    public async Task ANewEntryShowsUpInTheStats()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = StatsTab(budget);
        Assert.Equal("—", Tile(page, "avg-expenses"));

        await page.InvokeAsync(() => Services.GetRequiredService<CommandSender>().SendAsync(
            new RecordExpense(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, "Bread", "Food",
                new Money(12m, "PLN", 1m, Today), On(9, 20), null)));

        page.WaitForAssertion(() => Assert.Equal("12.00 PLN", Tile(page, "avg-expenses")));
    }

    [Fact]
    public void TheStatsTabHasNoFab()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = StatsTab(budget);

        Assert.Empty(page.FindAll(".pspad-fab"));
    }

    [Fact]
    public void AnArchivedBudgetStillShowsItsStats()
    {
        var budget = BudgetsPageTests.Named("Old");
        var pay = MoneyEntries.Income(budget, "Pay", "Salary", 300m, On(9, 1));
        Balances.Archived(budget);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, pay);

        var page = StatsTab(budget);

        Assert.Equal("300.00 PLN", Tile(page, "avg-income"));
        Assert.Contains("This budget is archived", page.Markup);
        Assert.Empty(page.FindAll(".pspad-fab"));
    }

    [Fact]
    public void TheChartsFollowTheChosenRange()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Party", "Food", 70m, new DateOnly(2025, 12, 31)));
        var page = StatsTab(budget);
        Assert.Equal(6, page.FindAll(".pspad-stats-by-month table.mud-sr-only tbody tr").Count);

        Preset(page, "Last year").Click();

        page.WaitForAssertion(() =>
            Assert.Equal(12, page.FindAll(".pspad-stats-by-month table.mud-sr-only tbody tr").Count));
        var december = page.FindAll(".pspad-stats-by-month table.mud-sr-only tbody tr")[11];
        Assert.Equal(["December 2025", "0.00 PLN", "70.00 PLN", "−70.00 PLN"],
            december.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()));
    }
}
