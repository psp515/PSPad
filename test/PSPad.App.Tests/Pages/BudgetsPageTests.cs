using Bunit;
using PSPad.App.Pages;
using PSPad.Module.Money.Budgets;
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
