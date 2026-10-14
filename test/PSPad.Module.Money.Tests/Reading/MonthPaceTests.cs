using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class MonthPaceTests
{
    static DateOnly On(int month, int day) => new(2026, month, day);

    [Fact]
    public void TheCurrentMonthCountsTheDaysUpToToday()
    {
        var pace = MonthPace.For([], October, On(10, 9));

        Assert.Equal(9, pace.Day);
        Assert.Equal(31, pace.DaysInMonth);
        Assert.Equal(9m / 31m, pace.ShareOfMonthPassed);
    }

    [Fact]
    public void APastMonthIsWholeAndAFutureMonthHasNotStarted()
    {
        Assert.Equal(30, MonthPace.For([], new YearMonth(2026, 9), On(10, 9)).Day);
        Assert.Equal(0, MonthPace.For([], November, On(10, 9)).Day);
    }

    [Fact]
    public void ExpectedByNowProRatesTheUsualMonth()
    {
        var budget = ABudget();
        var entries = new[]
        {
            AnIncome(budget, "Salary", 3100m, On(9, 28)),
            AnExpense(budget, "Food", 620m, On(9, 3)),
            AnExpense(budget, "Food", 100m, On(10, 2))
        };

        var pace = MonthPace.For(entries, October, On(10, 10));

        Assert.Equal(1000m, pace.ExpectedIncomeByNow);
        Assert.Equal(200m, pace.ExpectedExpensesByNow);
        Assert.Equal(100m / 620m, pace.ShareOfUsualSpent);
        Assert.Equal(100m, pace.Totals.Expenses);
    }

    [Fact]
    public void WithoutABaselineNothingIsExpected()
    {
        var budget = ABudget();

        var pace = MonthPace.For([AnExpense(budget, "Food", 100m, On(10, 2))], October, On(10, 10));

        Assert.True(pace.Usual.IsEmpty);
        Assert.Null(pace.ExpectedIncomeByNow);
        Assert.Null(pace.ExpectedExpensesByNow);
        Assert.Null(pace.ShareOfUsualSpent);
        var food = Assert.Single(pace.Categories);
        Assert.Equal(("Food", 100m, (decimal?)null, (decimal?)null), (food.Category, food.Spent, food.UsualByNow, food.UsualMonth));
        Assert.Null(food.Delta);
    }

    [Fact]
    public void CategoriesUniteThisMonthAndTheUsualOnesSortedBySpentThenUsual()
    {
        var budget = ABudget();
        var entries = new[]
        {
            AnExpense(budget, "Food", 310m, On(9, 3)),
            AnExpense(budget, "Car", 620m, On(9, 4)),
            AnExpense(budget, "Gifts", 50m, On(10, 2)),
            AnExpense(budget, "food", 150m, On(10, 3))
        };

        var pace = MonthPace.For(entries, October, On(10, 10));

        Assert.Equal(["Food", "Gifts", "Car"], pace.Categories.Select(category => category.Category));
        var food = pace.Categories[0];
        Assert.Equal((150m, 100m, 310m, 50m), (food.Spent, food.UsualByNow, food.UsualMonth, food.Delta));
        var car = pace.Categories[2];
        Assert.Equal((0m, 200m, 620m, -200m), (car.Spent, car.UsualByNow, car.UsualMonth, car.Delta));
        Assert.Null(pace.Categories[1].UsualByNow);
    }
}
