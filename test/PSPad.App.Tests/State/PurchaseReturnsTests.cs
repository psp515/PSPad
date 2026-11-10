using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class PurchaseReturnsTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void APurchaseWithReturnsReadsWhatWasReturned()
    {
        var budget = MoneyEntries.ABudget("Personal");
        var shoes = MoneyEntries.Expense(budget, "Shoes", "Clothes", 420m, Today);
        var first = MoneyEntries.Return(budget, shoes, 150m, Today);
        var second = MoneyEntries.Return(budget, shoes, 20m, Today);

        var state = new PurchaseReturns([shoes, first, second]).Of(shoes);

        Assert.NotNull(state);
        Assert.Equal(170m, state.Returned);
        Assert.Equal(250m, state.Left);
    }

    [Fact]
    public void OnlyAPurchaseWithLiveReturnsHasAState()
    {
        var budget = MoneyEntries.ABudget("Personal");
        var shoes = MoneyEntries.Expense(budget, "Shoes", "Clothes", 420m, Today);
        var bread = MoneyEntries.Expense(budget, "Bread", "Food", 5m, Today);
        var pay = MoneyEntries.Income(budget, "Pay", "Salary", 3000m, Today);
        var refund = MoneyEntries.Return(budget, shoes, 150m, Today);
        var returns = new PurchaseReturns([shoes, bread, pay, refund]);

        Assert.Null(returns.Of(bread));
        Assert.Null(returns.Of(pay));
        Assert.Null(returns.Of(refund));
    }
}
