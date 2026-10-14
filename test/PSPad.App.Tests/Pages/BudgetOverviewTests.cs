using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using PSPad.App.Pages;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetOverviewTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 10);

    static DateOnly On(int month, int day) => new(2026, month, day);

    IRenderedComponent<BudgetPage> Overview(Budget budget)
    {
        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => page.Find(".pspad-overview"));
        return page;
    }

    static string Text(IRenderedComponent<BudgetPage> page, string selector) => page.Find(selector).TextContent.Trim();

    [Fact]
    public void TheHeroComparesTheMonthSoFarWithTheUsualMonth()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Income(budget, "Pay", "Salary", 3100m, On(9, 28)),
            MoneyEntries.Expense(budget, "Rent", "Home", 620m, On(9, 1)),
            MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(10, 1)),
            MoneyEntries.Income(budget, "Pay", "Salary", 500m, On(10, 2)));

        var page = Overview(budget);

        Assert.Equal("October 2026 · day 10 of 31", Text(page, ".pspad-overview-day"));
        Assert.Equal("+200.00 PLN", Text(page, ".pspad-overview-net .pspad-money-value"));
        Assert.Equal("500.00 PLN", Text(page, ".pspad-overview-income .pspad-money-value"));
        Assert.Equal("usual by now 1,000.00 PLN", Text(page, ".pspad-overview-income-usual"));
        Assert.Equal("300.00 PLN", Text(page, ".pspad-overview-expenses .pspad-money-value"));
        Assert.Equal("+100.00 PLN vs usual by now", Text(page, ".pspad-overview-expenses-delta"));
        Assert.Equal("≈ 620.00 PLN", Text(page, ".pspad-overview-expected .pspad-money-value"));
        Assert.Equal("avg of last 1 months", Text(page, ".pspad-overview-expected-caption"));
        Assert.Equal("Spent 48% of a usual month · 32% of the month passed", Text(page, ".pspad-overview-pace-caption"));
        Assert.Single(page.FindAll(".pspad-category-legend"));
    }

    [Fact]
    public void WithoutABaselineTheHeroSaysAveragesComeLater()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget,
            MoneyEntries.Expense(budget, "Rent", "Home", 300m, On(10, 1)));

        var page = Overview(budget);

        Assert.Equal("Averages appear after a full month of entries.", Text(page, ".pspad-overview-no-usual"));
        Assert.Empty(page.FindAll(".pspad-overview-income-usual"));
        Assert.Empty(page.FindAll(".pspad-overview-expected"));
        Assert.Empty(page.FindAll(".pspad-overview-pace"));
    }
}
