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
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Preferences;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class HoldingPanelTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);
    static readonly YearMonth September = new(2026, 9);
    static readonly YearMonth October = new(2026, 10);
    static readonly DateOnly SeptemberLastDay = new(2026, 9, 30);
    static readonly DateOnly OctoberLastDay = new(2026, 10, 31);

    IRenderedComponent<HoldingPanel> Open(BalanceSnapshot snapshot, Guid? holdingId = null) =>
        Render<HoldingPanel>(parameters => parameters
            .Add(p => p.SnapshotId, (Guid?)snapshot.Id)
            .Add(p => p.HoldingId, holdingId));

    static IRenderedComponent<MudNumericField<decimal?>> Numeric(IRenderedComponent<HoldingPanel> panel, string css) =>
        panel.FindComponents<MudNumericField<decimal?>>().Single(field => field.Instance.Class!.Contains(css));

    static Task PickCurrencyAsync(IRenderedComponent<HoldingPanel> panel, string currency) =>
        panel.InvokeAsync(() => panel.FindComponents<MudSelect<string>>()
            .Single(select => select.Instance.Class!.Contains("pspad-holding-panel-currency-field"))
            .Instance.ValueChanged.InvokeAsync(currency));

    static async Task FillAsync(IRenderedComponent<HoldingPanel> panel, string name, decimal amount)
    {
        panel.Find(".pspad-holding-name-field input").Input(name);
        await panel.InvokeAsync(() => Numeric(panel, "pspad-holding-panel-amount-field").Instance.ValueChanged.InvokeAsync(amount));
    }

    static string InPln(IRenderedComponent<HoldingPanel> panel) =>
        panel.Find(".pspad-holding-panel-pln input").GetAttribute("value") ?? "";

    Task<T> SentAsync<T>() => AppTestHost.SentAsync<T>(this);

    AppTestHost.FakeNbpRates Nbp => (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();

    [Fact]
    public async Task ANewHoldingIsAddedToTheMonthInPln()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        var replica = AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october);
        Assert.Contains("New holding", panel.Markup);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));

        await FillAsync(panel, "Wallet", 250m);
        panel.Find(".pspad-panel-save").Click();

        var sent = await SentAsync<AddHolding>();
        Assert.Equal((budget.Id, October, "Wallet", HoldingType.Cash), (sent.BudgetId, sent.Month, sent.Name, sent.Type));
        Assert.Equal(new Money(250m, "PLN", 1m, Today), sent.Money);
        var stored = await replica.LoadAsync<BalanceSnapshot>(october.Id);
        Assert.Equal(new Money(250m, "PLN", 1m, OctoberLastDay), Assert.Single(stored!.Holdings).Money);
    }

    [Fact]
    public async Task TheTypeToggleSetsTheType()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october);
        await FillAsync(panel, "Konto", 1000m);

        panel.FindAll(".mud-toggle-item").Single(item => item.TextContent.Trim() == "Savings").Click();
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal(HoldingType.Savings, (await SentAsync<AddHolding>()).Type);
    }

    [Fact]
    public async Task TheCurrencyStartsFromTheDefaultAndTheRateFromTheLatestHolding()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var preferences = new MoneyPreferences();
        preferences.ApplyAll(MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), budget.UserId, "EUR"),
            DateTimeOffset.UnixEpoch));
        var september = Balances.Started(budget, September,
            Balances.AHolding("Konto", 10m, "EUR", 4.25m, rateDate: SeptemberLastDay));
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, preferences, september, october);
        var panel = Open(october);

        await FillAsync(panel, "Broker", 10m);

        Assert.Equal("42.50 PLN", InPln(panel));
        panel.Find(".pspad-panel-save").Click();
        Assert.Equal(new Money(10m, "EUR", 4.25m, SeptemberLastDay), (await SentAsync<AddHolding>()).Money);
    }

    [Fact]
    public async Task ATypedRateIsDatedTodayWhileTheMonthRuns()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october);
        await FillAsync(panel, "Broker", 10m);
        await PickCurrencyAsync(panel, "EUR");
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));

        await panel.InvokeAsync(() => Numeric(panel, "pspad-holding-panel-rate-field").Instance.ValueChanged.InvokeAsync(4.3m));
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal(new Money(10m, "EUR", 4.3m, Today), (await SentAsync<AddHolding>()).Money);
    }

    [Fact]
    public async Task ATypedRateInAnEndedMonthIsDatedItsLastDay()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var september = Balances.Started(budget, September);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, september);
        var panel = Open(september);
        await FillAsync(panel, "Broker", 10m);
        await PickCurrencyAsync(panel, "EUR");

        await panel.InvokeAsync(() => Numeric(panel, "pspad-holding-panel-rate-field").Instance.ValueChanged.InvokeAsync(4.3m));
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal(new Money(10m, "EUR", 4.3m, SeptemberLastDay), (await SentAsync<AddHolding>()).Money);
    }

    [Fact]
    public void TheRateFieldIsHiddenForPln()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);

        var panel = Open(october);

        Assert.Empty(panel.FindAll(".pspad-holding-panel-rate-field"));
        Assert.Empty(panel.FindAll(".pspad-holding-panel-nbp"));
    }

    [Fact]
    public async Task NbpAsksForTodayWhileTheMonthRuns()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        Nbp.Next = new NbpRateView("EUR", 4.2512m, new DateOnly(2026, 10, 8));
        var panel = Open(october);
        await FillAsync(panel, "Broker", 10m);
        await PickCurrencyAsync(panel, "EUR");

        panel.Find(".pspad-holding-panel-nbp").Click();
        panel.WaitForAssertion(() => Assert.False(panel.Find(".pspad-panel-save").HasAttribute("disabled")));
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal([("EUR", Today)], Nbp.Asked);
        Assert.Equal(new Money(10m, "EUR", 4.2512m, new DateOnly(2026, 10, 8)), (await SentAsync<AddHolding>()).Money);
    }

    [Fact]
    public async Task NbpAsksForTheLastDayOfAnEndedMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var september = Balances.Started(budget, September);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, september);
        Nbp.Next = new NbpRateView("EUR", 4.26m, SeptemberLastDay);
        var panel = Open(september);
        await FillAsync(panel, "Broker", 10m);
        await PickCurrencyAsync(panel, "EUR");

        panel.Find(".pspad-holding-panel-nbp").Click();

        panel.WaitForAssertion(() => Assert.Equal([("EUR", SeptemberLastDay)], Nbp.Asked));
    }

    [Fact]
    public async Task ALateNbpAnswerForAnotherCurrencyIsDropped()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        Nbp.Gate = new TaskCompletionSource<NbpRateView?>();
        var panel = Open(october);
        await FillAsync(panel, "Broker", 10m);
        await PickCurrencyAsync(panel, "EUR");
        panel.Find(".pspad-holding-panel-nbp").Click();

        await PickCurrencyAsync(panel, "USD");
        await panel.InvokeAsync(() => Nbp.Gate.SetResult(new NbpRateView("EUR", 4.2512m, new DateOnly(2026, 10, 8))));

        panel.WaitForAssertion(() => Assert.False(panel.Find(".pspad-holding-panel-nbp").HasAttribute("disabled")));
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ALateNbpAnswerForAnotherHoldingIsDropped()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var konto = Balances.AHolding("Konto", 10m, "EUR", 4.25m);
        var broker = Balances.AHolding("Broker", 5m, "EUR", 4.20m);
        var october = Balances.Started(budget, October, konto, broker);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        Nbp.Gate = new TaskCompletionSource<NbpRateView?>();
        var panel = Open(october, konto.HoldingId);
        panel.Find(".pspad-holding-panel-nbp").Click();

        panel.Render(parameters => parameters.Add(p => p.HoldingId, (Guid?)broker.HoldingId));
        panel.WaitForAssertion(() =>
            Assert.Equal("Broker", panel.Find(".pspad-holding-name-field input").GetAttribute("value")));
        await panel.InvokeAsync(() => Nbp.Gate.SetResult(new NbpRateView("EUR", 4.2512m, new DateOnly(2026, 10, 8))));

        Assert.Equal("21.00 PLN", InPln(panel));
    }

    [Fact]
    public async Task AFailedNbpLookupKeepsTheRateAndSaysSo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october);
        await FillAsync(panel, "Broker", 10m);
        await PickCurrencyAsync(panel, "EUR");

        panel.Find(".pspad-holding-panel-nbp").Click();

        panel.WaitForAssertion(() => Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == "Couldn't get the NBP rate. Type the rate instead."));
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task TheNbpButtonIsDisabledOffline()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        Services.AddSingleton<IConnectivity>(new AppTestHost.Offline());
        var panel = Open(october);

        await PickCurrencyAsync(panel, "EUR");

        Assert.True(panel.Find(".pspad-holding-panel-nbp").HasAttribute("disabled"));
    }

    [Fact]
    public async Task EditingFillsTheFieldsAndSendsEditHolding()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var wallet = Balances.AHolding("Wallet", 100m);
        var october = Balances.Started(budget, October, wallet);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october, wallet.HoldingId);
        Assert.DoesNotContain("New holding", panel.Markup);
        Assert.Equal("Wallet", panel.Find(".pspad-holding-name-field input").GetAttribute("value"));

        panel.Find(".pspad-holding-name-field input").Input("Cash box");
        panel.Find(".pspad-panel-save").Click();

        var sent = await SentAsync<EditHolding>();
        Assert.Equal((budget.Id, October, wallet.HoldingId, "Cash box"), (sent.BudgetId, sent.Month, sent.HoldingId, sent.Name));
        Assert.Equal(new Money(100m, "PLN", 1m, OctoberLastDay), sent.Money);
    }

    [Fact]
    public async Task RemoveSendsRemoveHolding()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var wallet = Balances.AHolding("Wallet", 100m);
        var october = Balances.Started(budget, October, wallet);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october, wallet.HoldingId);

        Assert.Equal("Remove", panel.Find(".pspad-panel-delete").TextContent.Trim());
        panel.Find(".pspad-panel-delete").Click();

        var sent = await SentAsync<RemoveHolding>();
        Assert.Equal((budget.Id, October, wallet.HoldingId), (sent.BudgetId, sent.Month, sent.HoldingId));
    }

    [Fact]
    public void AnArchivedBudgetOpensReadOnly()
    {
        var budget = BudgetsPageTests.Named("Old");
        var wallet = Balances.AHolding("Wallet", 100m);
        var october = Balances.Started(budget, October, wallet);
        AppTestHost.Arrange(this, budget.UserId, Today, Balances.Archived(budget), october);

        var panel = Open(october, wallet.HoldingId);

        Assert.True(panel.Find(".pspad-holding-name-field input").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
    }

    [Fact]
    public async Task ARejectionIsShownAndNothingIsQueued()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var panel = Open(october);

        await FillAsync(panel, new string('h', 65), 10m);
        panel.Find(".pspad-panel-save").Click();

        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message == "A holding name is at most 64 characters.");
        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.NotEmpty(panel.FindAll(".pspad-holding-name-field"));
    }

    [Fact]
    public void AnUnknownHoldingOpensNothing()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October, Balances.AHolding("Wallet", 100m));
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);

        var panel = Open(october, Guid.NewGuid());

        Assert.DoesNotContain("pspad-holding-name-field", panel.Markup);
    }

    [Fact]
    public void ADeletedMonthOpensNothing()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Deleted(Balances.Started(budget, October), budget);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);

        var panel = Open(october);

        Assert.DoesNotContain("pspad-holding-name-field", panel.Markup);
    }
}
