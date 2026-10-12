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
    public void AUrlWithABudgetOpensIt()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, BudgetQuery.From($"https://pspad.local/budgets?budget={id}"));
    }

    [Fact]
    public void ANewBudgetUrlIsNewAndOpensNoExistingBudget()
    {
        var uri = BudgetQuery.ForNew("https://pspad.local/budgets?task=" + Guid.NewGuid());

        Assert.Equal("https://pspad.local/budgets?budget=new", uri);
        Assert.True(BudgetQuery.IsNew(uri));
        Assert.Null(BudgetQuery.From(uri));
    }

    [Fact]
    public void EditingABudgetKeepsTheScreen()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"https://pspad.local/budgets?budget={id}", BudgetQuery.For("https://pspad.local/budgets", id));
    }
}
