using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class BudgetQueryTests
{
    [Fact]
    public void AUrlWithNoQueryOpensNoBudget()
    {
        Assert.Null(BudgetQuery.From("https://pspad.local/budgets"));
        Assert.False(BudgetQuery.IsNew("https://pspad.local/budgets"));
    }

    [Fact]
    public void ANewBudgetUrlIsNewAndOpensNoExistingBudget()
    {
        var uri = BudgetQuery.ForNew("https://pspad.local/budgets?task=" + Guid.NewGuid());

        Assert.Equal("https://pspad.local/budgets?budget=new", uri);
        Assert.True(BudgetQuery.IsNew(uri));
        Assert.Null(BudgetQuery.From(uri));
    }
}
