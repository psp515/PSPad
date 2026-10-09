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

        page.WaitForAssertion(() => Assert.Contains("Personal", page.Markup));
        Assert.DoesNotContain("Old", page.Markup);

        page.Find(".pspad-show-archived input").Change(true);

        page.WaitForAssertion(() => Assert.Contains("Old", page.Markup));
        Assert.NotEmpty(page.FindAll(".pspad-budget-card.pspad-archived"));
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
