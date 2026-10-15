using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Pages;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetBalanceTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);
    static readonly YearMonth August = new(2026, 8);
    static readonly YearMonth September = new(2026, 9);
    static readonly YearMonth October = new(2026, 10);

    static string ShownMonth(IRenderedComponent<BudgetPage> page) => page.Find(".pspad-month-chip[aria-pressed='true']").GetAttribute("aria-label")!;

    IRenderedComponent<BudgetPage> BalanceTab(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "balance"));
        page.WaitForAssertion(() => page.Find(".pspad-month-strip"));
        return page;
    }

    static RenderFragment WithPopovers(Guid budgetId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<BudgetPage>(1);
        builder.AddAttribute(2, nameof(BudgetPage.BudgetId), budgetId);
        builder.AddAttribute(3, nameof(BudgetPage.Tab), "balance");
        builder.CloseComponent();
    };

    AppTestHost.FakeNbpRates Nbp => (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();

    static BalanceSnapshot EuroAndZloty(Budget budget) => Balances.Started(budget, October,
        Balances.AHolding("Konto", 10m, "EUR", 4.30m, HoldingType.Savings),
        Balances.AHolding("Broker", 5m, "EUR", 4.20m, HoldingType.Investment),
        Balances.AHolding("Wallet", 100m));

    [Fact]
    public void TheNbpTilesValueHoldingsAtTodaysRateAskingOncePerCurrency()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, EuroAndZloty(budget));
        Nbp.ByCurrency["EUR"] = new NbpRateView("EUR", 4.40m, new DateOnly(2026, 10, 8));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("166.00 PLN", Text(page, ".pspad-balance-at-nbp .pspad-money-value")));
        Assert.Equal("164.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-value"));
        Assert.Equal("+2.00 PLN", Text(page, ".pspad-balance-currency .pspad-money-value"));
        Assert.Equal(["—", "+1.00 PLN", "+1.00 PLN"],
            page.FindAll(".pspad-holding-today").Select(cell => cell.TextContent.Trim()));
        Assert.Equal([("EUR", Today)], Nbp.Asked);
    }

    [Fact]
    public void ALowerRateTodayShowsANegativeDifference()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Konto", 10m, "EUR", 4.30m)));
        Nbp.ByCurrency["EUR"] = new NbpRateView("EUR", 4.20m, Today);

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("−1.00 PLN", Text(page, ".pspad-balance-currency .pspad-money-value")));
        Assert.Equal("−1.00 PLN", Text(page, ".pspad-holding-today"));
    }

    [Fact]
    public void OfflineTheNbpTilesAreHiddenAndNothingIsAsked()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, EuroAndZloty(budget));
        Services.AddSingleton<IConnectivity>(new AppTestHost.Offline());

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => page.Find(".pspad-balance-net-worth"));
        Assert.Empty(page.FindAll(".pspad-balance-at-nbp"));
        Assert.Empty(page.FindAll(".pspad-balance-currency"));
        Assert.Equal(["—", "—", "—"], page.FindAll(".pspad-holding-today").Select(cell => cell.TextContent.Trim()));
        Assert.Empty(Nbp.Asked);
    }

    [Fact]
    public void ComingOnlineAsksForTheRatesAnOfflineVisitSkipped()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, EuroAndZloty(budget));
        var connectivity = new AppTestHost.SwitchableConnectivity();
        Services.AddSingleton<IConnectivity>(connectivity);
        Nbp.ByCurrency["EUR"] = new NbpRateView("EUR", 4.40m, Today);
        var page = BalanceTab(budget);
        page.WaitForAssertion(() => page.Find(".pspad-balance-net-worth"));
        Assert.Empty(Nbp.Asked);

        page.InvokeAsync(connectivity.GoOnline);

        page.WaitForAssertion(() => Assert.Equal("166.00 PLN", Text(page, ".pspad-balance-at-nbp .pspad-money-value")));
        Assert.Equal([("EUR", Today)], Nbp.Asked);
    }

    [Fact]
    public void LeavingThePageUnsubscribesFromConnectivity()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, EuroAndZloty(budget));
        var connectivity = new AppTestHost.SwitchableConnectivity();
        Services.AddSingleton<IConnectivity>(connectivity);
        var page = BalanceTab(budget);
        page.WaitForAssertion(() => page.Find(".pspad-balance-net-worth"));
        var before = connectivity.Subscribers;

        page.Render(parameters => parameters.Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.True(connectivity.Subscribers < before));
    }

    [Fact]
    public void AFailedLookupHidesTheNbpTilesAndIsNotRetriedDuringTheVisit()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, EuroAndZloty(budget));

        var page = BalanceTab(budget);
        page.WaitForAssertion(() => page.Find(".pspad-balance-net-worth"));
        page.Find(".pspad-month-chip[aria-label='September 2026']").Click();
        page.Find(".pspad-month-chip[aria-label='October 2026']").Click();

        page.WaitForAssertion(() => page.Find(".pspad-balance-net-worth"));
        Assert.Empty(page.FindAll(".pspad-balance-at-nbp"));
        Assert.Equal([("EUR", Today)], Nbp.Asked);
        Assert.DoesNotContain(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message?.Contains("NBP") == true);
    }

    [Fact]
    public void APlnOnlyBalanceShowsTheNbpTilesWithoutAsking()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("100.00 PLN", Text(page, ".pspad-balance-at-nbp .pspad-money-value")));
        Assert.Equal("0.00 PLN", Text(page, ".pspad-balance-currency .pspad-money-value"));
        Assert.Empty(Nbp.Asked);
    }

    [Fact]
    public async Task AnAnswerForAnotherCurrencyIsDropped()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Konto", 10m, "EUR", 4.30m)));
        Nbp.Gate = new TaskCompletionSource<NbpRateView?>();
        var page = BalanceTab(budget);

        await page.InvokeAsync(() => Nbp.Gate.SetResult(new NbpRateView("USD", 3.90m, Today)));

        page.WaitForAssertion(() => Assert.Equal("—", Text(page, ".pspad-holding-today")));
        Assert.Empty(page.FindAll(".pspad-balance-at-nbp"));
    }

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    Task<T> SentAsync<T>() => AppTestHost.SentAsync<T>(this);

    [Fact]
    public void AMonthWithoutABalanceOffersToStartIt()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = BalanceTab(budget);

        Assert.Equal("October 2026", ShownMonth(page));
        Assert.Contains("October 2026 has no balance yet", Text(page, ".pspad-balance-missing"));
        Assert.Equal("Start October 2026", Text(page, ".pspad-start-month"));
        Assert.Empty(page.FindAll(".pspad-fab"));
        Assert.Empty(page.FindAll(".pspad-balance-menu"));
        Assert.Empty(page.FindAll(".pspad-balance-net-worth"));
    }

    [Fact]
    public async Task StartingTheMonthCarriesTheLatestBalanceForward()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, September, Balances.AHolding("Wallet", 100m)));
        var page = BalanceTab(budget);

        page.Find(".pspad-start-month").Click();

        var sent = await SentAsync<StartBalanceMonth>();
        Assert.Equal((budget.Id, October), (sent.BudgetId, sent.Month));
        page.WaitForAssertion(() => Assert.Equal("Wallet", Text(page, ".pspad-holding-name")));
        Assert.Empty(page.FindAll(".pspad-balance-missing"));
        Assert.Equal("100.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-value"));
    }

    [Fact]
    public void TheTilesShowNetWorthAndTheChangeSinceTheLatestEarlierBalance()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, August, Balances.AHolding("Wallet", 100m)),
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m),
                Balances.AHolding("Konto", 10m, "EUR", 4.3m, HoldingType.Savings)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("143.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-value")));
        Assert.Equal("+43.00 PLN", Text(page, ".pspad-balance-change .pspad-money-value"));
        Assert.Contains("vs August 2026", Text(page, ".pspad-balance-change"));
    }

    [Fact]
    public void WithoutAnEarlierBalanceTheChangeIsADash()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("—", Text(page, ".pspad-balance-change .pspad-money-value")));
        Assert.Contains("vs previous month", Text(page, ".pspad-balance-change"));
    }

    [Fact]
    public void HoldingsShowNameTypeAmountRateAndPln()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Konto", 10m, "EUR", 4.3m, HoldingType.Savings)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".pspad-holding-row")));
        Assert.Equal("Konto", Text(page, ".pspad-holding-name"));
        Assert.Equal("Savings", Text(page, ".pspad-holding-group-name"));
        Assert.Equal("43.00 PLN", Text(page, ".pspad-holding-group-total"));
        Assert.Equal("10.00 EUR @ 4.3000", Text(page, ".pspad-holding-sub"));
        Assert.Equal("10.00 EUR", Text(page, ".pspad-holding-amount"));
        Assert.Equal("4.3000", Text(page, ".pspad-holding-rate"));
        Assert.Equal("43.00 PLN", Text(page, ".pspad-holding-pln"));
    }

    [Fact]
    public void HoldingsAreGroupedByTypeWithATypeBar()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October,
                Balances.AHolding("Broker", 300m, "PLN", 1m, HoldingType.Investment),
                Balances.AHolding("Wallet", 100m, "PLN", 1m, HoldingType.Cash)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-holding-row").Count));
        Assert.Equal(["Cash", "Investment"], page.FindAll(".pspad-holding-group-name").Select(name => name.TextContent.Trim()));
        Assert.Equal(["Wallet", "Broker"], page.FindAll(".pspad-holding-name").Select(name => name.TextContent.Trim()));
        Assert.Equal(2, page.FindAll(".pspad-balance-types .pspad-category-legend").Count);
    }

    [Fact]
    public void AStartedMonthWithoutHoldingsSaysSo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, Balances.Started(budget, October));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("No holdings in October 2026.", Text(page, ".pspad-balance-empty")));
        Assert.Equal("0.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-value"));
    }

    [Fact]
    public void TappingAHoldingOpensItsPanel()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var konto = Balances.AHolding("Konto", 10m, "EUR", 4.3m);
        var october = Balances.Started(budget, October, konto);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}/balance");
        var page = BalanceTab(budget);
        page.WaitForAssertion(() => page.Find(".pspad-holding-row"));

        page.Find(".pspad-holding-row").Click();

        Assert.EndsWith($"/budgets/{budget.Id}/balance?balance={october.Id}&holding={konto.HoldingId}", navigation.Uri);
    }

    [Fact]
    public void TheFabAddsAHoldingToAStartedMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var october = Balances.Started(budget, October);
        AppTestHost.Arrange(this, budget.UserId, Today, budget, october);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budgets/{budget.Id}/balance");
        var page = BalanceTab(budget);
        page.WaitForAssertion(() => page.Find(".pspad-fab"));

        Assert.Equal("Month actions", page.Find(".pspad-fab[aria-label]").GetAttribute("aria-label"));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        var items = page.FindAll(".mud-fab-menu-item");
        Assert.Equal(["Add holding", "Delete month"], items.Select(item => item.GetAttribute("aria-label")));
        page.Find(".pspad-fab-add-holding").Click();

        Assert.EndsWith($"/budgets/{budget.Id}/balance?balance={october.Id}&holding=new", navigation.Uri);
    }

    [Fact]
    public void AMonthWithoutABalanceKeepsTheLayoutAndSaysWhatStartingDoes()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, September, Balances.AHolding("Wallet", 100m)));

        var page = BalanceTab(budget);

        Assert.Empty(page.FindAll(".mud-alert"));
        Assert.NotNull(page.Find(".pspad-balance-hero-item .pspad-balance-missing"));
        Assert.Contains("October 2026 has no balance yet", Text(page, ".pspad-balance-missing"));
        Assert.Contains("copies the holdings from September 2026", Text(page, ".pspad-balance-missing"));
        Assert.Equal("Start October 2026", Text(page, ".pspad-balance-missing .pspad-start-month"));
        Assert.Equal("No holdings in October 2026.", Text(page, ".pspad-balance-holdings .pspad-balance-empty"));
        Assert.NotNull(page.Find(".pspad-balance-history"));
    }

    [Fact]
    public void AFirstMonthWithoutABalanceAsksForHoldings()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = BalanceTab(budget);

        Assert.Contains("Start it and add your holdings.", Text(page, ".pspad-balance-missing"));
        Assert.Empty(page.FindAll(".pspad-balance-history"));
    }

    [Fact]
    public async Task DeletingTheMonthSendsDeleteBalanceMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));
        var page = Render(WithPopovers(budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-fab .mud-fab-menu-button"));

        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.Find(".pspad-delete-month").Click();

        var sent = await SentAsync<DeleteBalanceMonth>();
        Assert.Equal((budget.Id, October), (sent.BudgetId, sent.Month));
        page.WaitForAssertion(() => page.Find(".pspad-balance-missing"));
    }

    [Fact]
    public void AnArchivedBudgetShowsItsBalanceReadOnly()
    {
        var budget = BudgetsPageTests.Named("Old");
        var october = Balances.Started(budget, October, Balances.AHolding("Wallet", 100m));
        AppTestHost.Arrange(this, budget.UserId, Today, Balances.Archived(budget), october);

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => page.Find(".pspad-holding-row"));
        Assert.Empty(page.FindAll(".pspad-fab"));
        Assert.Empty(page.FindAll(".pspad-balance-menu"));
    }

    [Fact]
    public void AnArchivedBudgetCannotStartAMonth()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => page.Find(".pspad-balance-missing"));
        Assert.Empty(page.FindAll(".pspad-start-month"));
    }

    [Fact]
    public void ADeletedMonthHasNoBalance()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Deleted(Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)), budget));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => page.Find(".pspad-balance-missing"));
        Assert.Empty(page.FindAll(".pspad-holding-row"));
    }

    [Fact]
    public void TheSwitcherShowsEarlierBalances()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, September, Balances.AHolding("Wallet", 75m)));
        var page = BalanceTab(budget);

        page.Find(".pspad-month-chip[aria-label='September 2026']").Click();

        page.WaitForAssertion(() => Assert.Equal("September 2026", ShownMonth(page)));
        Assert.Equal("75.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-value"));
    }

    [Fact]
    public void AnotherBudgetsBalanceIsNotShown()
    {
        var budget = BudgetsPageTests.Named("Personal");
        var other = BudgetsPageTests.Named("Other");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, other,
            Balances.Started(other, October, Balances.AHolding("Theirs", 5m)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => page.Find(".pspad-balance-missing"));
    }

    [Fact]
    public void TheHistoryChartStacksTheLastTwelveMonthsByType()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, August, Balances.AHolding("Wallet", 80m), Balances.AHolding("Bank", 20m, type: HoldingType.Savings)),
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));

        var page = BalanceTab(budget);

        var chart = page.FindComponents<MudChart<double>>().Single(chart => chart.Instance.Class?.Contains("pspad-balance-history-chart") == true);
        Assert.Equal(ChartType.StackedBar, chart.Instance.ChartType);
        Assert.Equal(12, chart.Instance.ChartLabels.Length);
        var cash = chart.Instance.ChartSeries.Single(series => series.Name == "Cash").Data.Values.ToArray();
        Assert.Equal(80d, cash[9]);
        Assert.Equal(0d, cash[10]);
        Assert.Equal(100d, cash[11]);
        Assert.Equal(["Month", "Cash", "Savings", "Investment", "Total"],
            page.FindAll(".pspad-balance-history table.mud-sr-only thead th").Select(cell => cell.TextContent.Trim()));
    }

    [Fact]
    public void WithoutAnyBalanceThereIsNoHistoryChart()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = BalanceTab(budget);

        Assert.Empty(page.FindAll(".pspad-balance-history"));
    }

    [Fact]
    public void TheNetWorthColumnSitsBesideTheHoldingsOnlyWhenWide()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => page.Find(".pspad-holding-row"));
        Assert.Contains("mud-grid-item-lg-4", page.Find(".pspad-balance-aside").ClassList);
        Assert.DoesNotContain("mud-grid-item-md-4", page.Find(".pspad-balance-aside").ClassList);
        Assert.Contains("mud-grid-item-lg-8", page.Find(".pspad-balance-holdings").ClassList);
        Assert.Contains("mud-grid-item-md-5", page.Find(".pspad-balance-hero-item").ClassList);
        Assert.Contains("mud-grid-item-lg-12", page.Find(".pspad-balance-hero-item").ClassList);
        Assert.Contains("mud-grid-item-md-7", page.Find(".pspad-balance-history-item").ClassList);
        Assert.Contains("mud-grid-item-lg-12", page.Find(".pspad-balance-history-item").ClassList);
        Assert.NotNull(page.Find(".pspad-balance-history-item .pspad-balance-history"));
    }
}
