using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Pages;
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

    IRenderedComponent<BudgetPage> BalanceTab(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "balance"));
        page.WaitForAssertion(() => page.Find(".pspad-month-title"));
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

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    Task<T> SentAsync<T>() => AppTestHost.SentAsync<T>(this);

    [Fact]
    public void AMonthWithoutABalanceOffersToStartIt()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = BalanceTab(budget);

        Assert.Equal("October 2026", Text(page, ".pspad-month-title"));
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
        Assert.Equal("100.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-tile-value"));
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

        page.WaitForAssertion(() => Assert.Equal("143.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-tile-value")));
        Assert.Equal("+43.00 PLN", Text(page, ".pspad-balance-change .pspad-money-tile-value"));
        Assert.Contains("vs August 2026", Text(page, ".pspad-balance-change"));
    }

    [Fact]
    public void WithoutAnEarlierBalanceTheChangeIsADash()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("—", Text(page, ".pspad-balance-change .pspad-money-tile-value")));
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
        Assert.Equal("Savings", Text(page, ".pspad-holding-type"));
        Assert.Equal("10.00 EUR", Text(page, ".pspad-holding-amount"));
        Assert.Equal("4.3000", Text(page, ".pspad-holding-rate"));
        Assert.Equal("43.00 PLN", Text(page, ".pspad-holding-pln"));
    }

    [Fact]
    public void AStartedMonthWithoutHoldingsSaysSo()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget, Balances.Started(budget, October));

        var page = BalanceTab(budget);

        page.WaitForAssertion(() => Assert.Equal("No holdings in October 2026.", Text(page, ".pspad-balance-empty")));
        Assert.Equal("0.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-tile-value"));
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

        Assert.Equal("Add holding", page.Find(".pspad-fab").GetAttribute("aria-label"));
        page.Find(".pspad-fab").Click();

        Assert.EndsWith($"/budgets/{budget.Id}/balance?balance={october.Id}&holding=new", navigation.Uri);
    }

    [Fact]
    public async Task DeletingTheMonthSendsDeleteBalanceMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            Balances.Started(budget, October, Balances.AHolding("Wallet", 100m)));
        var page = Render(WithPopovers(budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-balance-menu button"));

        page.Find(".pspad-balance-menu button").Click();
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

        page.Find(".pspad-month-previous").Click();

        page.WaitForAssertion(() => Assert.Equal("September 2026", Text(page, ".pspad-month-title")));
        Assert.Equal("75.00 PLN", Text(page, ".pspad-balance-net-worth .pspad-money-tile-value"));
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
}
