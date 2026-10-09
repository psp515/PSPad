using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Pages;
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

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

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
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => Assert.Contains("Eating Out", page.Markup));

        page.Find(".pspad-category-list-expense .pspad-category-add").Click();
        page.Find(".pspad-category-list-expense .pspad-category-input input").Input("Hobby");
        page.Find(".pspad-category-list-expense .pspad-category-input input").KeyDown("Enter");

        var outbox = Services.GetRequiredService<IOutbox>();
        var entry = Assert.Single(await outbox.PeekAsync(10));
        Assert.Equal(nameof(AddCategory), entry.Envelope.Type);
    }

    [Fact]
    public void AnArchivedBudgetShowsABannerAndHidesAdd()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

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
}
