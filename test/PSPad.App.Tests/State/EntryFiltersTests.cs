using PSPad.App.State;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class EntryFiltersTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void NoneShowsEverything()
    {
        var budget = Pages.BudgetsPageTests.Named("Personal");

        Assert.True(EntryFilters.None.Shows(Pages.MoneyEntries.Expense(budget, "Rolls", "Food", 3m, Today)));
    }

    [Fact]
    public void KindCategoryAndSearchNarrowTheList()
    {
        var budget = Pages.BudgetsPageTests.Named("Personal");
        var rolls = Pages.MoneyEntries.Expense(budget, "Rolls", "Food", 3m, Today);
        var pay = Pages.MoneyEntries.Income(budget, "Pay", "Salary", 3000m, Today);

        Assert.False(EntryFilters.None with { Kind = EntryFilter.Income } is var income && income.Shows(rolls));
        Assert.True(income.Shows(pay));
        Assert.True((EntryFilters.None with { Category = "food" }).Shows(rolls));
        Assert.False((EntryFilters.None with { Category = "food" }).Shows(pay));
        Assert.True((EntryFilters.None with { Search = " ROLL " }).Shows(rolls));
        Assert.False((EntryFilters.None with { Search = "pay" }).Shows(rolls));
    }
}
