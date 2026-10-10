using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Layout;
using PSPad.App.State.Outbox;
using PSPad.App.Sync;
using PSPad.App.Tests.Pages;
using PSPad.Contracts;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Preferences;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class EntryPanelTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);

    IRenderedComponent<EntryPanel> NewExpense(Budget budget) =>
        Render<EntryPanel>(parameters => parameters
            .Add(p => p.NewInBudget, (Guid?)budget.Id)
            .Add(p => p.NewKind, CategoryKind.Expense));

    static async Task FillAsync(IRenderedComponent<EntryPanel> panel, string name, string category, decimal amount)
    {
        panel.Find(".pspad-entry-name-field input").Input(name);
        var autocomplete = panel.FindComponent<MudAutocomplete<string>>();
        await panel.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(category));
        await panel.InvokeAsync(() => Numeric(panel, "pspad-entry-amount-field").Instance.ValueChanged.InvokeAsync(amount));
    }

    static IRenderedComponent<MudNumericField<decimal?>> Numeric(IRenderedComponent<EntryPanel> panel, string css) =>
        panel.FindComponents<MudNumericField<decimal?>>().Single(field => field.Instance.Class!.Contains(css));

    static Task PickCurrencyAsync(IRenderedComponent<EntryPanel> panel, string currency) =>
        panel.InvokeAsync(() => panel.FindComponents<MudSelect<string>>()
            .Single(select => select.Instance.Class!.Contains("pspad-entry-currency-field"))
            .Instance.ValueChanged.InvokeAsync(currency));

    Task<T> SentAsync<T>() => AppTestHost.SentAsync<T>(this);

    AppTestHost.FakeNbpRates Nbp => (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();

    [Fact]
    public async Task ANewExpenseIsRecordedOnTheUsersTodayInPln()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var panel = NewExpense(budget);
        Assert.Contains("New expense", panel.Markup);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));

        await FillAsync(panel, "Groceries", "Food", 42.5m);
        panel.Find(".pspad-panel-save").Click();

        var sent = await SentAsync<RecordExpense>();
        Assert.Equal((budget.Id, "Groceries", "Food", Today), (sent.BudgetId, sent.Name, sent.Category, sent.Date));
        Assert.Equal(new Money(42.5m, "PLN", 1m, Today), sent.Money);
    }

    [Fact]
    public async Task TheRateFieldIsHiddenForPlnAndShownForOtherCurrencies()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var panel = NewExpense(budget);
        Assert.Empty(panel.FindAll(".pspad-entry-rate-field"));

        await PickCurrencyAsync(panel, "EUR");

        Assert.NotEmpty(panel.FindAll(".pspad-entry-rate-field"));
    }

    [Fact]
    public async Task TheCurrencyStartsFromTheDefaultCurrencyAndNeedsARate()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var preferences = new MoneyPreferences();
        preferences.ApplyAll(MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), budget.UserId, "EUR"),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, budget.UserId, Today, budget, preferences);
        var panel = NewExpense(budget);

        await FillAsync(panel, "Train", "Car", 10m);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        await panel.InvokeAsync(() => Numeric(panel, "pspad-entry-rate-field").Instance.ValueChanged.InvokeAsync(4.3m));
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal(new Money(10m, "EUR", 4.3m, Today), (await SentAsync<RecordExpense>()).Money);
    }

    [Fact]
    public async Task TheRateIsPrefilledFromTheBudgetsLatestRate()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Hotel", "Home", 100m, new DateOnly(2026, 10, 5), "EUR", 4.30m));
        var panel = NewExpense(budget);

        await FillAsync(panel, "Train", "Car", 10m);
        await PickCurrencyAsync(panel, "EUR");

        Assert.Equal("43.00 PLN", panel.Find(".pspad-entry-pln input").GetAttribute("value"));
        panel.Find(".pspad-panel-save").Click();
        Assert.Equal(new Money(10m, "EUR", 4.30m, new DateOnly(2026, 10, 5)), (await SentAsync<RecordExpense>()).Money);
    }

    [Fact]
    public async Task AnotherBudgetsRateIsNotSuggested()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var other = BudgetsPageTests.Named("Other");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, other,
            MoneyEntries.Expense(other, "Hotel", "Home", 100m, new DateOnly(2026, 10, 5), "EUR", 4.30m));
        var panel = NewExpense(budget);

        await FillAsync(panel, "Train", "Car", 10m);
        await PickCurrencyAsync(panel, "EUR");

        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task TheNbpButtonIsDisabledOffline()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        Services.AddSingleton<IConnectivity>(new Offline());
        var panel = NewExpense(budget);

        await PickCurrencyAsync(panel, "EUR");

        Assert.True(panel.Find(".pspad-entry-nbp").HasAttribute("disabled"));
    }

    [Fact]
    public async Task NbpFillsTheRateAndItsDate()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        Nbp.Next = new NbpRateView("EUR", 4.2512m, new DateOnly(2026, 10, 8));
        var panel = NewExpense(budget);
        await FillAsync(panel, "Train", "Car", 10m);
        await PickCurrencyAsync(panel, "EUR");

        panel.Find(".pspad-entry-nbp").Click();
        panel.WaitForAssertion(() => Assert.False(panel.Find(".pspad-panel-save").HasAttribute("disabled")));
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal([("EUR", Today)], Nbp.Asked);
        Assert.Equal(new Money(10m, "EUR", 4.2512m, new DateOnly(2026, 10, 8)), (await SentAsync<RecordExpense>()).Money);
    }

    [Fact]
    public async Task ANbpFailureKeepsTheRateAndSaysSo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Hotel", "Home", 100m, new DateOnly(2026, 10, 5), "EUR", 4.30m));
        Nbp.Next = null;
        var panel = NewExpense(budget);
        await FillAsync(panel, "Train", "Car", 10m);
        await PickCurrencyAsync(panel, "EUR");

        panel.Find(".pspad-entry-nbp").Click();

        panel.WaitForAssertion(() => Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == "Couldn't get the NBP rate. Type the rate instead."));
        panel.Find(".pspad-panel-save").Click();
        Assert.Equal(4.30m, (await SentAsync<RecordExpense>()).Money.RateToPln);
    }

    [Fact]
    public void TypingAnUnknownCategoryOffersToCreateIt()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<EntryPanel>(1);
            builder.AddAttribute(2, nameof(EntryPanel.NewInBudget), (Guid?)budget.Id);
            builder.AddAttribute(3, nameof(EntryPanel.NewKind), CategoryKind.Expense);
            builder.CloseComponent();
        });

        page.Find(".pspad-entry-category-field input").Input("Hobby");

        page.WaitForAssertion(() => Assert.Contains(page.FindAll(".mud-popover *"),
            element => element.TextContent.Trim() == "Create category \"Hobby\""));
    }

    [Fact]
    public async Task SavingUnderANewCategoryAddsItToTheBudget()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var replica = AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var panel = NewExpense(budget);

        await FillAsync(panel, "Lego", "Hobby", 99m);
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal("Hobby", (await SentAsync<RecordExpense>()).Category);
        Assert.Contains("Hobby", (await replica.LoadAsync<Budget>(budget.Id))!.ExpenseCategories);
    }

    [Fact]
    public async Task EditingSendsEditEntry()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var entry = MoneyEntries.Expense(budget, "Groceries", "Food", 10m, Today);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, entry);
        var panel = Render<EntryPanel>(parameters => parameters.Add(p => p.EntryId, (Guid?)entry.Id));
        Assert.Contains("Expense", panel.Find(".pspad-panel-title").TextContent);
        Assert.NotEmpty(panel.FindAll(".pspad-panel-delete"));

        await panel.InvokeAsync(() => Numeric(panel, "pspad-entry-amount-field").Instance.ValueChanged.InvokeAsync(20m));
        panel.Find(".pspad-panel-save").Click();

        var sent = await SentAsync<EditEntry>();
        Assert.Equal((entry.Id, "Groceries", "Food"), (sent.EntryId, sent.Name, sent.Category));
        Assert.Equal(new Money(20m, "PLN", 1m, Today), sent.Money);
    }

    [Fact]
    public async Task DeletingSendsDeleteEntry()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var entry = MoneyEntries.Expense(budget, "Groceries", "Food", 10m, Today);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, entry);
        var panel = Render<EntryPanel>(parameters => parameters.Add(p => p.EntryId, (Guid?)entry.Id));

        panel.Find(".pspad-panel-delete").Click();

        Assert.Equal(entry.Id, (await SentAsync<DeleteEntry>()).EntryId);
    }

    [Fact]
    public void AnEntryOfAnArchivedBudgetIsReadOnly()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var entry = MoneyEntries.Expense(budget, "Groceries", "Food", 10m, Today);
        budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), budget.UserId, budget.Id), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, budget.UserId, Today, budget, entry);

        var panel = Render<EntryPanel>(parameters => parameters.Add(p => p.EntryId, (Guid?)entry.Id));

        Assert.Contains("Groceries", panel.Markup);
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
    }

    [Fact]
    public async Task ARejectedSaveStaysOpenAndSaysWhy()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var panel = NewExpense(budget);

        await FillAsync(panel, new string('n', 121), "Food", 5m);
        panel.Find(".pspad-panel-save").Click();

        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == "An entry name is at most 120 characters.");
        Assert.NotEmpty(panel.FindAll(".pspad-panel-save"));
    }

    sealed class Offline : IConnectivity
    {
        public bool IsOnline => false;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }
}
