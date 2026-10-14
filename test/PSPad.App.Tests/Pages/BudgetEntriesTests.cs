using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetEntriesTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);

    static DateOnly On(int month, int day) => new(2026, month, day);

    static DateTimeOffset At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    IRenderedComponent<BudgetPage> Entries(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "entries"));
        page.WaitForAssertion(() => page.Find(".pspad-entries"));
        return page;
    }

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    static string[] Names(IRenderedComponent<BudgetPage> page) =>
        [.. page.FindAll(".pspad-entry-name").Select(name => name.TextContent.Trim())];

    static IElement Chip(IRenderedComponent<BudgetPage> page, string text) =>
        page.FindAll(".pspad-entry-filter .mud-chip").Single(chip => chip.TextContent.Trim() == text);

    static MoneyEntry[] October(Budget budget) =>
    [
        MoneyEntries.Expense(budget, "Groceries", "Food", 100m, On(10, 2), at: At(10, 2, 9)),
        MoneyEntries.Expense(budget, "Train", "Car", 10m, On(10, 5), "EUR", 4.3m, At(10, 7, 9)),
        MoneyEntries.Income(budget, "Salary", "Salary", 1000m, On(10, 1), at: At(10, 1, 9)),
        MoneyEntries.Expense(budget, "Old", "Food", 5m, On(9, 30), at: At(10, 1, 8))
    ];

    [Fact]
    public void TheMonthListsEntriesByDayWithTheirRecordedTimeAndTotals()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, [budget, .. October(budget)]);

        var page = Entries(budget);

        Assert.Equal(["Train", "Groceries", "Salary"], Names(page));
        Assert.Equal("3 entries · income 1,000.00 · expenses 143.00 · net +857.00 PLN", Text(page, ".pspad-entries-caption"));
        Assert.Equal(["7 Oct 09:00", "2 Oct 09:00", "1 Oct 09:00"],
            page.FindAll(".pspad-entry-recorded").Select(recorded => recorded.TextContent.Trim().Replace("recorded ", "")));
        Assert.Equal(["−43.00", "−100.00", "+1,000.00"], page.FindAll(".pspad-entry-day-net").Select(net => net.TextContent.Trim()));
        Assert.Empty(page.FindAll(".pspad-money-hero"));
    }

    [Fact]
    public void TheKindChipsNarrowTheListAndTheCaption()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, [budget, .. October(budget)]);
        var page = Entries(budget);

        Chip(page, "Income").Click();

        Assert.Equal(["Salary"], Names(page));
        Assert.Equal("1 entry · income 1,000.00 · expenses 0.00 · net +1,000.00 PLN", Text(page, ".pspad-entries-caption"));
    }

    [Fact]
    public void TappingACategoryFiltersToItAndTheChipClearsIt()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, [budget, .. October(budget)]);
        var page = Entries(budget);

        page.FindAll(".pspad-entry-category-open").Single(button => button.TextContent.Trim() == "Food").Click();

        Assert.Equal("Food", Text(page, ".pspad-entries-category"));
        Assert.Equal(["Groceries"], Names(page));

        page.Find(".pspad-entries-category .mud-chip-close-button").Click();

        Assert.Empty(page.FindAll(".pspad-entries-category"));
        Assert.Equal(3, Names(page).Length);
    }

    [Fact]
    public void SearchMatchesNameOrNote()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, [budget, .. October(budget)]);
        var page = Entries(budget);

        page.Find(".pspad-entries-search input").Input("train");

        Assert.Equal(["Train"], Names(page));

        page.Find(".pspad-entries-search input").Input("zzz");

        Assert.Equal("No entries match.", Text(page, ".pspad-entries-caption"));
    }

    [Fact]
    public async Task SortingByRecordedGroupsByTheDayTheEntryWasRecorded()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, [budget, .. October(budget)]);
        var page = Entries(budget);

        await page.InvokeAsync(() => page.FindComponent<MudSelect<EntrySort>>().Instance.ValueChanged.InvokeAsync(EntrySort.Recorded));

        Assert.Equal(["Recorded Wednesday, 7 October", "Recorded Friday, 2 October", "Recorded Thursday, 1 October"],
            page.FindAll(".pspad-entry-day-title").Select(title => title.TextContent.Trim()));
        Assert.Equal(["Train", "Groceries", "Salary"], Names(page));
    }

    [Fact]
    public void AnEmptyMonthSaysSo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Entries(budget);

        Assert.Equal("No entries in October 2026.", Text(page, ".pspad-entries-caption"));
        Assert.Empty(page.FindAll(".pspad-entry-filter"));
    }

    [Fact]
    public void TheQuickActionsAddToTheShownMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}/entries");
        var page = Entries(budget);

        page.Find(".pspad-month-previous").Click();
        page.Find(".pspad-budget-quick-expense").Click();

        Assert.EndsWith($"/budgets/{budget.Id}/entries?expense={budget.Id}&month=2026-09", navigation.Uri);
    }
}
