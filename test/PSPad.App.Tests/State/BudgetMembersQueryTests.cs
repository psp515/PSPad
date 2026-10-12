using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class BudgetMembersQueryTests
{
    [Fact]
    public void AUrlWithMembersOpensThatBudgetsPanel()
    {
        var id = Guid.NewGuid();
        var uri = BudgetMembersQuery.For("https://pspad.local/budgets/x?entry=" + Guid.NewGuid(), id);

        Assert.Equal($"https://pspad.local/budgets/x?members={id}", uri);
        Assert.Equal(id, BudgetMembersQuery.From(uri));
    }

    [Fact]
    public void AUrlWithoutMembersOpensNothing() => Assert.Null(BudgetMembersQuery.From("https://pspad.local/budgets"));
}
