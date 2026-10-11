using Bunit;
using PSPad.App.Pages;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetsPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void WithNoBudgetsItOffersToCreateOne()
    {
        AppTestHost.Arrange(this, User, Today);

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Contains("No budgets yet.", page.Markup));
        Assert.Contains("Create budget", page.Markup);
    }

    [Fact]
    public void ItShowsActiveBudgetsAndHidesArchivedUntilAsked()
    {
        AppTestHost.Arrange(this, User, Today, Named("Personal"), Named("Old", archived: true));

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Equal(["Personal"], CardNames(page)));

        page.Find(".pspad-show-archived input").Change(true);

        page.WaitForAssertion(() => Assert.Equal(["Old", "Personal"], CardNames(page).Order()));
        Assert.NotEmpty(page.FindAll(".pspad-budget-card.pspad-archived"));
    }

    static string[] CardNames(IRenderedComponent<BudgetsPage> page) =>
        [.. page.FindAll(".pspad-budget-card-name").Select(name => name.TextContent.Trim())];

    [Fact]
    public void ACardShowsThisMonthsTotalsInPln()
    {
        var budget = Named("Personal");
        AppTestHost.Arrange(this, User, Today, budget,
            MoneyEntries.Income(budget, "Salary", "Salary", 1000m, new DateOnly(2026, 10, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 250m, new DateOnly(2026, 10, 2)),
            MoneyEntries.Expense(budget, "Old", "Food", 99m, new DateOnly(2026, 9, 30)));

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Equal("1,000.00 PLN", page.Find(".pspad-budget-card-income").TextContent.Trim()));
        Assert.Equal("250.00 PLN", page.Find(".pspad-budget-card-expenses").TextContent.Trim());
        Assert.Equal("+750.00 PLN", page.Find(".pspad-budget-card-net").TextContent.Trim());
        Assert.NotEmpty(page.FindAll(".pspad-budget-card-bar"));
    }

    [Fact]
    public void ACardCountsOnlyItsOwnBudgetsEntries()
    {
        var busy = Named("Busy");
        var quiet = Named("Quiet");
        AppTestHost.Arrange(this, User, Today, busy, quiet,
            MoneyEntries.Expense(busy, "Rent", "Home", 250m, new DateOnly(2026, 10, 2)),
            MoneyEntries.Expense(quiet, "Old", "Food", 99m, new DateOnly(2026, 9, 30)));

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".pspad-budget-card-expenses")));
        Assert.Equal("No entries in October 2026", Assert.Single(page.FindAll(".pspad-budget-card-empty")).TextContent.Trim());
    }

    [Fact]
    public void ACardLinksToItsBudget()
    {
        var budget = Named("Personal");
        AppTestHost.Arrange(this, User, Today, budget);

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll($"a[href='/budgets/{budget.Id}']")));
    }

    [Fact]
    public void ACardShowsTheLatestNetWorthUpToThisMonth()
    {
        var budget = Named("Personal");
        AppTestHost.Arrange(this, User, Today, budget,
            Balances.Started(budget, new YearMonth(2026, 9), Balances.AHolding("Wallet", 100m),
                Balances.AHolding("Konto", 10m, "EUR", 4.3m)),
            Balances.Started(budget, new YearMonth(2026, 11), Balances.AHolding("Wallet", 999m)));

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Equal("143.00 PLN", page.Find(".pspad-budget-card-net-worth").TextContent.Trim()));
        Assert.Contains("Net worth · September 2026", page.Find(".pspad-budget-card-balance").TextContent);
        Assert.Empty(page.FindAll(".pspad-budget-card-start"));
    }

    [Fact]
    public void ACardWithoutABalanceOffersToStartThisMonth()
    {
        var budget = Named("Personal");
        AppTestHost.Arrange(this, User, Today, budget);

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => page.Find(".pspad-budget-card-start"));
        var start = page.Find(".pspad-budget-card-start");
        Assert.Equal("Start October 2026", start.TextContent.Trim());
        Assert.Equal($"/budgets/{budget.Id}/balance", start.GetAttribute("href"));
        Assert.Empty(page.FindAll(".pspad-budget-card-net-worth"));
    }

    [Fact]
    public void ADeletedBalanceDoesNotCount()
    {
        var budget = Named("Personal");
        AppTestHost.Arrange(this, User, Today, budget,
            Balances.Deleted(Balances.Started(budget, new YearMonth(2026, 10), Balances.AHolding("Wallet", 100m)), budget));

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => page.Find(".pspad-budget-card-start"));
        Assert.Empty(page.FindAll(".pspad-budget-card-net-worth"));
    }

    [Fact]
    public void ACardCountsOnlyItsOwnBudgetsBalance()
    {
        var mine = Named("Mine");
        var other = Named("Other");
        AppTestHost.Arrange(this, User, Today, mine, other,
            Balances.Started(other, new YearMonth(2026, 10), Balances.AHolding("Theirs", 5m)));

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Equal("5.00 PLN", Assert.Single(page.FindAll(".pspad-budget-card-net-worth")).TextContent.Trim()));
        Assert.Single(page.FindAll(".pspad-budget-card-start"));
    }

    [Fact]
    public void AnArchivedCardWithoutABalanceSaysSo()
    {
        AppTestHost.Arrange(this, User, Today, Named("Old", archived: true));
        var page = Render<BudgetsPage>();

        page.Find(".pspad-show-archived input").Change(true);

        page.WaitForAssertion(() => Assert.Equal("No balance yet", page.Find(".pspad-budget-card-no-balance").TextContent.Trim()));
        Assert.Empty(page.FindAll(".pspad-budget-card-start"));
    }

    internal static Budget Named(string name, bool archived = false)
    {
        var id = Guid.NewGuid();
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), User, id, name), DateTimeOffset.UnixEpoch));
        if (archived)
        {
            budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), User, id), DateTimeOffset.UnixEpoch));
        }

        return budget;
    }
}
