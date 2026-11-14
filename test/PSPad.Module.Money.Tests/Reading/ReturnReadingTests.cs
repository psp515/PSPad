using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class ReturnReadingTests
{
    static readonly YearMonth OctoberMonth = new(2026, 10);
    static readonly YearMonth NovemberMonth = new(2026, 11);
    static readonly YearMonth DecemberMonth = new(2026, 12);

    static DateOnly On(int month, int day) => new(2026, month, day);

    static MoneyEntry[] Scenario()
    {
        var budget = ABudget();
        var headphones = AnExpense(budget, "PC", date: On(10, 20), money: new Money(100m, "EUR", 4.00m, On(10, 20)), name: "Headphones");
        var shoes = AnExpense(budget, "Clothes", 420m, On(11, 3), name: "Shoes");
        var rent = AnExpense(budget, "Home", 3200m, On(11, 2), name: "Rent");
        var shoesBack = AReturn(shoes, budget, 150m, On(11, 5), [shoes]);
        var headphonesBack = AReturn(headphones, budget, 50m, On(11, 10), [headphones]);
        var salary = AnIncome(budget, "Salary", 14200m, On(11, 2));
        return [headphones, shoes, rent, shoesBack, headphonesBack, salary];
    }

    [Fact]
    public void AMonthNetsReturnsDatedInIt()
    {
        var totals = MonthTotals.For(Scenario(), NovemberMonth);

        Assert.Equal(3270m, totals.Expenses);
        Assert.Equal(350m, totals.Returned);
        Assert.Equal(14200m, totals.Income);
        Assert.Equal(10930m, totals.Net);
    }

    [Fact]
    public void CategoriesRankAfterReturns()
    {
        var totals = MonthTotals.For(Scenario(), NovemberMonth);

        Assert.Equal([("Home", 3200m), ("Clothes", 270m), ("PC", -200m)], totals.ExpensesByCategory.Select(total => (total.Category, total.Pln)));
    }

    [Fact]
    public void ADayWithOnlyAReturnIsPositive()
    {
        var totals = MonthTotals.For(Scenario(), NovemberMonth);

        Assert.Equal(150m, totals.Days.Single(day => day.Date == On(11, 5)).Net);
    }

    [Fact]
    public void AReturnDoesNotTouchThePurchaseMonth()
    {
        var totals = MonthTotals.For(Scenario(), OctoberMonth);

        Assert.Equal(400m, totals.Expenses);
        Assert.Equal(0m, totals.Returned);
    }

    [Fact]
    public void ASummaryNetsReturnsInTotalsMonthsAndRanking()
    {
        var summary = MoneySummary.For(Scenario(), [], On(10, 1), On(11, 30));

        Assert.Equal(3670m, summary.Expenses);
        Assert.Equal(3270m, summary.PerMonth.Single(month => month.Month == NovemberMonth).Expenses);
        Assert.Equal(200m, summary.Ranking.Single(total => total.Category == "PC").Pln);
    }

    [Fact]
    public void TheUsualMonthNetsReturns()
    {
        var usual = UsualMonth.For(Scenario(), DecemberMonth);

        Assert.Equal((400m + 3270m) / 2, usual.Expenses);
    }

    [Fact]
    public void AReturnNeverSuggestsItsOwnRate()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, date: On(10, 20), money: new Money(100m, "EUR", 4.00m, On(10, 20)));
        var back = AReturn(purchase, budget, 50m, On(10, 25), [purchase], Now.AddDays(30));

        var suggestion = RateSuggestion.For("EUR", [purchase, back]);

        Assert.Equal(4.00m, suggestion!.RateToPln);
        Assert.Equal(On(10, 20), suggestion.RateDate);
    }

    [Fact]
    public void AReturnOfADeletedPurchaseSuggestsNothing()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, date: On(10, 20), money: new Money(100m, "EUR", 4.00m, On(10, 20)));
        var back = AReturn(purchase, budget, 50m, On(10, 25), [purchase]);
        Delete(purchase, budget);

        Assert.Null(RateSuggestion.For("EUR", [purchase, back]));
    }
}
