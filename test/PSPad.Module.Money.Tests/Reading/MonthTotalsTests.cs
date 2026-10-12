using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class MonthTotalsTests
{
    static readonly YearMonth October = new(2026, 10);

    static DateOnly Oct(int day) => new(2026, 10, day);

    [Fact]
    public void TotalsAreInPlnAndNetIsIncomeMinusExpenses()
    {
        var budget = ABudget();
        var totals = MonthTotals.For(
        [
            AnExpense(budget, amount: 100m, date: Oct(2)),
            AnExpense(budget, money: new Money(10m, "EUR", 4.3m, Oct(3)), date: Oct(3)),
            AnIncome(budget, amount: 1000m, date: Oct(1))
        ], October);

        Assert.Equal(1000m, totals.Income);
        Assert.Equal(143m, totals.Expenses);
        Assert.Equal(857m, totals.Net);
    }

    [Fact]
    public void OnlyTheMonthsOwnDaysCount()
    {
        var budget = ABudget();
        var totals = MonthTotals.For(
        [
            AnExpense(budget, amount: 1m, date: new DateOnly(2026, 9, 30)),
            AnExpense(budget, amount: 2m, date: Oct(1)),
            AnExpense(budget, amount: 4m, date: Oct(31)),
            AnExpense(budget, amount: 8m, date: new DateOnly(2026, 11, 1))
        ], October);

        Assert.Equal(6m, totals.Expenses);
        Assert.Equal([Oct(31), Oct(1)], totals.Days.Select(day => day.Date));
    }

    [Fact]
    public void DeletedEntriesDoNotCount()
    {
        var budget = ABudget();
        var totals = MonthTotals.For([Delete(AnExpense(budget, amount: 50m), budget), AnExpense(budget, amount: 5m)], October);

        Assert.Equal(5m, totals.Expenses);
        Assert.Single(Assert.Single(totals.Days).Entries);
    }

    [Fact]
    public void ExpensesPerCategoryAreSortedByValueDescending()
    {
        var budget = ABudget();
        var totals = MonthTotals.For(
        [
            AnExpense(budget, "Food", 50m),
            AnExpense(budget, "Car", 120m),
            AnExpense(budget, "Car", 80m),
            AnExpense(budget, "Home", 100m),
            AnIncome(budget, "Salary", 5000m)
        ], October);

        Assert.Equal(
            [new CategoryTotal("Car", 200m), new CategoryTotal("Home", 100m), new CategoryTotal("Food", 50m)],
            totals.ExpensesByCategory);
    }

    [Fact]
    public void EntriesAreGroupedByDayNewestFirstAndNewestRecordedFirstWithinADay()
    {
        var budget = ABudget();
        var morning = AnExpense(budget, date: Oct(3), at: Now);
        var evening = AnExpense(budget, date: Oct(3), at: Now.AddHours(9));
        var earlier = AnExpense(budget, date: Oct(1));

        var totals = MonthTotals.For([morning, earlier, evening], October);

        Assert.Equal([Oct(3), Oct(1)], totals.Days.Select(day => day.Date));
        Assert.Equal([evening.Id, morning.Id], totals.Days[0].Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void AnEmptyMonthHasNothing()
    {
        var totals = MonthTotals.For([], October);

        Assert.Equal(0m, totals.Income);
        Assert.Equal(0m, totals.Net);
        Assert.Empty(totals.ExpensesByCategory);
        Assert.Empty(totals.Days);
    }
}
