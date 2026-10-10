using Bunit;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetPageTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void ItShowsBothCategoryLists()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));
        Assert.Contains("Freelance", page.Markup);
        Assert.Contains("Expense categories", page.Markup);
        Assert.Contains("Income categories", page.Markup);
    }

    [Fact]
    public async Task AddingAnExpenseCategorySendsAddCategory()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));
        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));

        page.Find(".pspad-category-list-expense .pspad-category-add").Click();
        page.Find(".pspad-category-list-expense .pspad-category-input input").Input("Hobby");
        page.Find(".pspad-category-list-expense .pspad-category-input input").KeyDown("Enter");

        var outbox = Services.GetRequiredService<IOutbox>();
        var entry = Assert.Single(await outbox.PeekAsync(10));
        Assert.Equal(nameof(AddCategory), entry.Envelope.Type);
        var payload = entry.Envelope.Payload.Deserialize<AddCategory>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(CategoryKind.Expense, payload.Kind);
        Assert.Equal("Hobby", payload.Name);
    }

    [Fact]
    public void AnArchivedBudgetShowsABannerAndHidesAdd()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.Contains("This budget is archived", page.Markup));
        Assert.Empty(page.FindAll(".pspad-category-add"));
        Assert.NotEmpty(page.FindAll(".pspad-restore-budget"));
    }

    [Fact]
    public void AnUnknownBudgetSaysSo()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Today);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Contains("That budget does not exist.", page.Markup));
    }

    [Fact]
    public async Task ARejectedCategoryKeepsTheInputOpenWithItsTextAndShowsTheRejection()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));
        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));
        var tooLong = new string('c', 41);

        page.Find(".pspad-category-list-expense .pspad-category-add").Click();
        page.Find(".pspad-category-list-expense .pspad-category-input input").Input(tooLong);
        page.Find(".pspad-category-list-expense .pspad-category-input input").KeyDown("Enter");

        Assert.Equal(tooLong, page.Find(".pspad-category-list-expense .pspad-category-input input").GetAttribute("value"));
        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message?.Contains("A category name is at most 40 characters.") == true);
    }

    [Fact]
    public void TheCategoryInputCapsAtFortyCharacters()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));
        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));

        page.Find(".pspad-category-list-expense .pspad-category-add").Click();

        Assert.Equal("40", page.Find(".pspad-category-list-expense .pspad-category-input input").GetAttribute("maxlength"));
    }

    [Fact]
    public void ABudgetOwnedByAnotherUserDoesNotExist()
    {
        var budget = BudgetsPageTests.Named("Theirs");
        AppTestHost.Arrange(this, Guid.NewGuid(), Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => Assert.Contains("That budget does not exist.", page.Markup));
    }

    static RenderFragment WithPopovers(Guid budgetId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<BudgetPage>(1);
        builder.AddAttribute(2, nameof(BudgetPage.BudgetId), budgetId);
        builder.CloseComponent();
    };

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    [Fact]
    public void TheMonthTabIsTheDefaultAndShowsTheCurrentMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => Assert.Equal("October 2026", Text(page, ".pspad-month-title")));
        Assert.Contains("No entries in October 2026.", page.Markup);
        Assert.Empty(page.FindAll(".pspad-category-list"));
    }

    [Fact]
    public void TheSettingsRouteOpensTheSettingsTab()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".pspad-category-list")));
        Assert.Empty(page.FindAll(".pspad-month-title"));
    }

    [Fact]
    public void PickingATabPutsItInTheAddress()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}");
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-month-title"));

        page.FindAll(".mud-tab")[1].Click();

        Assert.EndsWith($"/budgets/{budget.Id}/settings", navigation.Uri);
    }

    [Fact]
    public void TheMonthShowsTotalsTheCategoryBarAndEntriesByDay()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Groceries", "Food", 100m, new DateOnly(2026, 10, 2)),
            MoneyEntries.Expense(budget, "Train", "Car", 10m, new DateOnly(2026, 10, 5), "EUR", 4.3m),
            MoneyEntries.Income(budget, "Salary", "Salary", 1000m, new DateOnly(2026, 10, 1)),
            MoneyEntries.Expense(budget, "Old", "Food", 5m, new DateOnly(2026, 9, 30)));

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".pspad-entry-row").Count));
        Assert.Equal("1,000.00 PLN", Text(page, ".pspad-month-income .pspad-money-tile-value"));
        Assert.Equal("143.00 PLN", Text(page, ".pspad-month-expenses .pspad-money-tile-value"));
        Assert.Equal("+857.00 PLN", Text(page, ".pspad-month-net .pspad-money-tile-value"));
        Assert.Equal(["Train", "Groceries", "Salary"], page.FindAll(".pspad-entry-name").Select(name => name.TextContent.Trim()));
        Assert.Equal(2, page.FindAll(".pspad-category-legend").Count);
        Assert.Contains("−43.00 PLN @ 4.3000", Assert.Single(page.FindAll(".pspad-entry-pln")).TextContent);
    }

    [Fact]
    public void TheSwitcherMovesBetweenMonths()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Old", "Food", 5m, new DateOnly(2026, 9, 30)));
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-month-title"));

        page.Find(".pspad-month-previous").Click();

        page.WaitForAssertion(() => Assert.Equal("September 2026", Text(page, ".pspad-month-title")));
        Assert.Equal("Old", Text(page, ".pspad-entry-name"));

        page.Find(".pspad-month-next").Click();

        page.WaitForAssertion(() => Assert.Equal("October 2026", Text(page, ".pspad-month-title")));
    }

    [Fact]
    public void TheCurrentMonthComesFromTheUsersStoredToday()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, new DateOnly(2026, 11, 1), budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => Assert.Equal("November 2026", Text(page, ".pspad-month-title")));
    }

    [Fact]
    public void TappingAnEntryOpensItsPanel()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var entry = MoneyEntries.Expense(budget, "Groceries", "Food", 100m, Today);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, entry);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}");
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-entry-row"));

        page.Find(".pspad-entry-row").Click();

        Assert.EndsWith($"/budgets/{budget.Id}?entry={entry.Id}", navigation.Uri);
    }

    [Fact]
    public void TheFabMenuOffersExpenseAndIncome()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}");
        var page = Render(WithPopovers(budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-fab"));

        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        var items = page.FindAll(".mud-fab-menu-item");
        Assert.Equal(["Add expense", "Add income"], items.Select(item => item.GetAttribute("aria-label")));
        items[1].Click();

        Assert.EndsWith($"/budgets/{budget.Id}?income={budget.Id}", navigation.Uri);
    }

    [Fact]
    public void AnArchivedBudgetHasNoFab()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => page.Find(".pspad-month-title"));
        Assert.Empty(page.FindAll(".pspad-fab"));
    }

    [Fact]
    public void TheSettingsTabHasNoFab()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => page.Find(".pspad-category-list"));
        Assert.Empty(page.FindAll(".pspad-fab"));
    }
}
