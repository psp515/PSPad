using System.Globalization;
using Bunit;
using PSPad.App.Pages;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
[Collection("Culture")]
public class BudgetsLocalizationTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void TheEmptyBudgetsPageSpeaksPolishUnderThePolishCulture()
    {
        AppTestHost.Arrange(this, User, Today);
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

            var page = Render<BudgetsPage>();

            page.WaitForAssertion(() => Assert.Contains("Nie masz jeszcze budżetów.", page.Markup));
            Assert.DoesNotContain("No budgets yet", page.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }

    [Fact]
    public void ABudgetCardSpeaksPolishWithPolishAmountsAndMonthNames()
    {
        var budget = BudgetsPageTests.Named("Osobisty");
        AppTestHost.Arrange(this, BudgetsPageTests.User, Today, budget,
            MoneyEntries.Income(budget, "Pensja", "Pensja", 1000m, new DateOnly(2026, 10, 1)),
            MoneyEntries.Expense(budget, "Czynsz", "Dom", 250m, new DateOnly(2026, 10, 2)));
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

            var page = Render<BudgetsPage>();

            page.WaitForAssertion(() => Assert.Equal("250,00 PLN", page.Find(".pspad-budget-card-expenses").TextContent.Trim()));
            Assert.Contains("Przychody", page.Find(".pspad-budget-card").TextContent);
            Assert.Contains("1 budżet · zarchiwizowane: 0 · sumy w PLN", page.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
