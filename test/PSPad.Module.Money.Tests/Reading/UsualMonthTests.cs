using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class UsualMonthTests
{
    static DateOnly On(int month, int day) => new(2026, month, day);

    [Fact]
    public void TheWindowIsTheSixMonthsBeforeTheCurrentOne()
    {
        var usual = UsualMonth.For([], October);

        Assert.Equal(new YearMonth(2026, 4), usual.From);
        Assert.Equal(new YearMonth(2026, 9), usual.To);
        Assert.True(usual.IsEmpty);
        Assert.Equal(0, usual.SampledMonths);
        Assert.Equal(0m, usual.Expenses);
        Assert.Empty(usual.ExpenseCategories);
    }

    [Fact]
    public void AveragesSkipMonthsWithoutEntriesAndIgnoreTheCurrentMonth()
    {
        var budget = ABudget();

        var usual = UsualMonth.For(
        [
            AnExpense(budget, "Food", 100m, On(4, 3)),
            AnExpense(budget, "Food", 300m, On(9, 3)),
            AnIncome(budget, "Salary", 1000m, On(9, 28)),
            AnExpense(budget, "Food", 999m, On(10, 1)),
            AnExpense(budget, "Food", 999m, On(3, 31))
        ], October);

        Assert.Equal(2, usual.SampledMonths);
        Assert.Equal(200m, usual.Expenses);
        Assert.Equal(500m, usual.Income);
        Assert.Equal(300m, usual.Saved);
        Assert.False(usual.IsEmpty);
    }

    [Fact]
    public void CategoriesAverageOverTheSampledMonthsAndSortByAverage()
    {
        var budget = ABudget();

        var usual = UsualMonth.For(
        [
            AnExpense(budget, "Food", 100m, On(8, 3)),
            AnExpense(budget, "food", 200m, On(9, 3)),
            AnExpense(budget, "Car", 500m, On(9, 4)),
            AnExpense(budget, "Gifts", 10m, On(9, 5))
        ], October);

        Assert.Equal(["Car", "Food", "Gifts"], usual.ExpenseCategories.Select(category => category.Category));
        Assert.Equal([250m, 150m, 5m], usual.ExpenseCategories.Select(category => category.Average));
        Assert.Equal([1, 2, 1], usual.ExpenseCategories.Select(category => category.MonthsSeen));
        Assert.Equal(150m, usual.Expense("FOOD")!.Average);
        Assert.Null(usual.Expense("Rent"));
    }

    [Fact]
    public void TheTypicalDayIsTheLowerMedianDayOfMonth()
    {
        var budget = ABudget();

        var usual = UsualMonth.For(
        [
            AnIncome(budget, "Salary", 1000m, On(6, 28)),
            AnIncome(budget, "Salary", 1000m, On(7, 27)),
            AnIncome(budget, "Salary", 1000m, On(8, 30)),
            AnIncome(budget, "Salary", 1000m, On(9, 2))
        ], October);

        var salary = Assert.Single(usual.IncomeCategories);
        Assert.Equal(27, salary.TypicalDay);
        Assert.Equal(4, salary.MonthsSeen);
    }

    [Fact]
    public void DeletedEntriesDoNotCount()
    {
        var budget = ABudget();

        var usual = UsualMonth.For([Delete(AnExpense(budget, "Food", 100m, On(9, 3)), budget)], October);

        Assert.True(usual.IsEmpty);
    }

    [Fact]
    public void AveragesRoundToWholeGrosze()
    {
        var budget = ABudget();

        var usual = UsualMonth.For(
            [AnExpense(budget, "Food", 1m, On(7, 3)), AnExpense(budget, "Food", 1m, On(8, 3)), AnExpense(budget, "Car", 1m, On(9, 3))],
            October);

        Assert.Equal(1m, usual.Expenses);
        Assert.Equal(0.67m, usual.Expense("Food")!.Average);
    }
}
