using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
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

        Assert.Equal(["Month", "Balance", "Summary"], Views(page).Select(view => view.TextContent.Trim()));
        Views(page)[1].Click();
        Assert.EndsWith($"/budgets/{budget.Id}/balance", navigation.Uri);

        Views(page)[2].Click();
        Assert.EndsWith($"/budgets/{budget.Id}/summary", navigation.Uri);

        page.Find(".pspad-budget-settings").Click();
        Assert.EndsWith($"/budgets/{budget.Id}/settings", navigation.Uri);
    }

    static IReadOnlyList<IElement> Views(IRenderedComponent<BudgetPage> page) =>
        page.FindAll(".pspad-budget-views .mud-toggle-item");

    [Fact]
    public void TheSettingsRouteSelectsNoViewAndPressesTheGear()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".pspad-category-list")));
        Assert.Empty(page.FindAll(".pspad-budget-views .mud-toggle-item-selected"));
        Assert.Equal("true", page.Find(".pspad-budget-settings").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void TheHeadingLeadsBackToTheBudgets()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => page.Find(".pspad-month-title"));
        Assert.Equal(BudgetLinks.Index, Services.GetRequiredService<PageHeader>().BackHref);
    }

    [Theory]
    [InlineData("month", true)]
    [InlineData("balance", true)]
    [InlineData("summary", false)]
    [InlineData("settings", false)]
    public void TheMonthSwitcherSharesTheViewRowOnlyForMonthAndBalance(string tab, bool shown)
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, tab));

        page.WaitForAssertion(() => page.Find(".pspad-budget-nav"));
        Assert.Equal(shown ? 1 : 0, page.FindAll(".pspad-budget-nav .pspad-month-switcher").Count);
        Assert.Equal(shown ? 1 : 0, page.FindAll(".pspad-month-switcher").Count);
    }

    [Fact]
    public void TheHeadingHasNoCategoryCountSubtitle()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => page.Find(".pspad-month-title"));
        Assert.Null(Services.GetRequiredService<PageHeader>().Subtitle);
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
        Assert.Equal("1,000.00 PLN", Text(page, ".pspad-month-income .pspad-money-value"));
        Assert.Equal("143.00 PLN", Text(page, ".pspad-month-expenses .pspad-money-value"));
        Assert.Equal("+857.00 PLN", Text(page, ".pspad-month-net .pspad-money-value"));
        Assert.Equal(["Train", "Groceries", "Salary"], page.FindAll(".pspad-entry-name").Select(name => name.TextContent.Trim()));
        Assert.Equal(2, page.FindAll(".pspad-category-legend").Count);
        Assert.Equal(["−43.00", "−100.00", "+1,000.00"], page.FindAll(".pspad-entry-amount").Select(amount => amount.TextContent.Trim()));
        Assert.Equal("10.00 EUR @ 4.3000", Assert.Single(page.FindAll(".pspad-entry-foreign")).TextContent.Trim());
        Assert.Equal(["Car", "Food", "Salary"], page.FindAll(".pspad-entry-category").Select(category => category.TextContent.Trim()));
        Assert.Equal(["C", "F", "S"], page.FindAll(".pspad-entry-avatar").Select(avatar => avatar.TextContent.Trim()));
        Assert.Single(page.FindAll(".pspad-entry-avatar.pspad-entry-income"));
        Assert.Equal(["−43.00", "−100.00", "+1,000.00"], page.FindAll(".pspad-entry-day-net").Select(net => net.TextContent.Trim()));
    }

    [Fact]
    public void TheFilterChipsShowOnlyExpensesOrOnlyIncome()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Groceries", "Food", 100m, new DateOnly(2026, 10, 2)),
            MoneyEntries.Income(budget, "Salary", "Salary", 1000m, new DateOnly(2026, 10, 2)),
            MoneyEntries.Income(budget, "Bonus", "Salary", 50m, new DateOnly(2026, 10, 1)));
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".pspad-entry-row").Count));
        Assert.Equal("true", Filter(page, "All").GetAttribute("aria-pressed"));

        Filter(page, "Expenses").Click();

        Assert.Equal(["Groceries"], page.FindAll(".pspad-entry-name").Select(name => name.TextContent.Trim()));
        Assert.Equal(["−100.00"], page.FindAll(".pspad-entry-day-net").Select(net => net.TextContent.Trim()));
        Assert.Equal("true", Filter(page, "Expenses").GetAttribute("aria-pressed"));

        Filter(page, "Income").Click();

        Assert.Equal(["Salary", "Bonus"], page.FindAll(".pspad-entry-name").Select(name => name.TextContent.Trim()));
        Assert.Equal("1,050.00 PLN", Text(page, ".pspad-month-income .pspad-money-value"));
    }

    static IElement Filter(IRenderedComponent<BudgetPage> page, string text) =>
        page.FindAll(".pspad-entry-filter .mud-chip").Single(chip => chip.TextContent.Trim() == text);

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

    IRenderedComponent<BudgetPage> Settings(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));
        page.WaitForAssertion(() => page.Find(".pspad-category-list"));
        return page;
    }

    static IElement Row(IRenderedComponent<BudgetPage> page, string list, string name) =>
        page.FindAll($".pspad-category-list-{list} .pspad-category-row")
            .Single(row => row.QuerySelector(".pspad-category-name")?.TextContent.Trim() == name);

    Task<T> SentAsync<T>() => AppTestHost.SentAsync<T>(this);

    [Fact]
    public async Task RenamingACategorySendsRenameCategoryAndRelabelsItsEntries()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var rolls = MoneyEntries.Expense(budget, "Rolls", "Food", 3m, Today);
        var replica = AppTestHost.Arrange(this, budget.UserId, Today, budget, rolls);
        var page = Settings(budget);

        Row(page, "expense", "Food").QuerySelector(".pspad-category-rename")!.Click();
        page.Find(".pspad-category-list-expense .pspad-category-rename-input input").Input("Groceries");
        page.Find(".pspad-category-list-expense .pspad-category-rename-input input").KeyDown("Enter");

        var sent = await SentAsync<RenameCategory>();
        Assert.Equal((CategoryKind.Expense, "Food", "Groceries"), (sent.Kind, sent.From, sent.To));
        Assert.Equal("Groceries", (await replica.LoadAsync<MoneyEntry>(rolls.Id))!.Category);
        page.WaitForAssertion(() => Row(page, "expense", "Groceries"));
    }

    [Fact]
    public async Task RenamingOntoAnotherCategoryKeepsTheInputAndSaysWhy()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Settings(budget);

        Row(page, "expense", "Home").QuerySelector(".pspad-category-rename")!.Click();
        page.Find(".pspad-category-list-expense .pspad-category-rename-input input").Input("food");
        page.Find(".pspad-category-list-expense .pspad-category-rename-input input").KeyDown("Enter");

        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == "That category already exists. Merge them instead.");
        Assert.Equal("food", page.Find(".pspad-category-list-expense .pspad-category-rename-input input").GetAttribute("value"));
    }

    [Fact]
    public async Task MergingACategorySendsMergeCategory()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Settings(budget);

        Row(page, "expense", "Bike").QuerySelector(".pspad-category-merge")!.Click();
        var target = page.FindComponents<MudSelect<string>>().Single(select => select.Instance.Class!.Contains("pspad-category-merge-target"));
        await page.InvokeAsync(() => target.Instance.ValueChanged.InvokeAsync("Car"));

        var sent = await SentAsync<MergeCategory>();
        Assert.Equal(("Bike", "Car"), (sent.From, sent.Into));
        page.WaitForAssertion(() => Assert.DoesNotContain(page.FindAll(".pspad-category-list-expense .pspad-category-name"),
            name => name.TextContent.Trim() == "Bike"));
    }

    [Fact]
    public async Task RemovingAnUnusedCategorySendsRemoveCategory()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Settings(budget);

        Row(page, "income", "Other").QuerySelector(".pspad-category-remove")!.Click();

        var sent = await SentAsync<RemoveCategory>();
        Assert.Equal((CategoryKind.Income, "Other"), (sent.Kind, sent.Name));
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll(".pspad-category-list-income .pspad-category-row").Count));
    }

    [Fact]
    public void AUsedCategoryShowsItsCountAndCannotBeRemoved()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Rolls", "Food", 3m, Today),
            MoneyEntries.Expense(budget, "Milk", "Food", 4m, Today),
            MoneyEntries.Expense(budget, "Lamp", "Home", 40m, Today));
        var page = Settings(budget);

        Assert.Contains("2 entries", Row(page, "expense", "Food").TextContent);
        Assert.Contains("1 entry", Row(page, "expense", "Home").TextContent);
        Assert.True(Row(page, "expense", "Food").QuerySelector(".pspad-category-remove")!.HasAttribute("disabled"));
        Assert.False(Row(page, "expense", "Car").QuerySelector(".pspad-category-remove")!.HasAttribute("disabled"));
        Assert.Null(Row(page, "expense", "Car").QuerySelector(".pspad-category-usage"));
    }

    [Fact]
    public void AnArchivedBudgetHidesCategoryActions()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Settings(budget);

        Assert.Empty(page.FindAll(".pspad-category-rename"));
        Assert.Empty(page.FindAll(".pspad-category-merge"));
        Assert.Empty(page.FindAll(".pspad-category-remove"));
    }

    [Fact]
    public async Task ArchivingSendsArchiveBudget()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Settings(budget);

        page.Find(".pspad-archive-budget").Click();

        Assert.Equal(budget.Id, (await SentAsync<ArchiveBudget>()).BudgetId);
    }

    [Fact]
    public async Task RestoringSendsRestoreBudget()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Settings(budget);

        page.Find(".pspad-restore-budget").Click();

        Assert.Equal(budget.Id, (await SentAsync<RestoreBudget>()).BudgetId);
    }

    [Fact]
    public void ARejectedArchiveSaysWhy()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        Services.AddSingleton<ICommandHandler<ArchiveBudget>>(new RejectingHandler<ArchiveBudget>("Not now."));
        var page = Settings(budget);

        page.Find(".pspad-archive-budget").Click();

        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars, snackbar => snackbar.Message == "Not now.");
    }

    [Fact]
    public void AnUnknownTabRendersTheMonthTab()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "nonsense"));

        page.WaitForAssertion(() => Assert.Equal("October 2026", Text(page, ".pspad-month-title")));
        Assert.Empty(page.FindAll(".pspad-category-list"));
    }

    [Fact]
    public void TheCategoryBarIsLabelledAndTheMonthButtonsHaveNames()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Groceries", "Food", 100m, Today));

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => page.Find(".pspad-category-bar-track"));
        var bar = page.Find(".pspad-category-bar-track");
        Assert.Equal("img", bar.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(bar.GetAttribute("aria-label")));
        Assert.Equal("Previous month", page.Find(".pspad-month-previous").GetAttribute("aria-label"));
        Assert.Equal("Next month", page.Find(".pspad-month-next").GetAttribute("aria-label"));
    }

    [Fact]
    public void SettingsOfferImportAndLinkTheTemplates()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));
        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("templates/pspad-entries.csv", page.Find(".pspad-budget-import-entries").InnerHtml);
        Assert.Contains("templates/pspad-balances.csv", page.Find(".pspad-budget-import-balances").InnerHtml);

        page.Find(".pspad-budget-import-entries .pspad-budget-import-open").Click();
        Assert.EndsWith($"/budgets/{budget.Id}/import/entries", navigation.Uri);

        page.Find(".pspad-budget-import-balances .pspad-budget-import-open").Click();
        Assert.EndsWith($"/budgets/{budget.Id}/import/balances", navigation.Uri);
    }

    [Fact]
    public void AnArchivedBudgetHidesTheImportCard()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));
        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));

        Assert.Empty(page.FindAll(".pspad-budget-import-card"));
        Assert.Empty(page.FindAll(".pspad-budget-import-open"));
    }
}
