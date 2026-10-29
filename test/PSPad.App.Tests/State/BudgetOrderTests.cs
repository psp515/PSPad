using PSPad.App.State;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class BudgetOrderTests
{
    const string Token = "abcdefghijklmnopqrstuvwxyz";
    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Other = Guid.NewGuid();
    static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    static Budget Owned(Guid owner, string name, DateTimeOffset at)
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), owner, Guid.NewGuid(), name), at));
        return budget;
    }

    static Budget JoinedBy(Budget budget, Guid member, DateTimeOffset at)
    {
        budget.ApplyAll(Budget.Decide(budget, new ShareBudget(Guid.NewGuid(), budget.UserId, budget.Id, Token, "K7M4PX", "O"), at));
        budget.ApplyAll(Budget.Decide(budget, new JoinBudget(Guid.NewGuid(), member, budget.Id, Token, "K7M4PX", "Me"), at));
        return budget;
    }

    [Fact]
    public void MineComeFirstByCreationThenSharedByJoining()
    {
        var mineLater = Owned(Me, "Later", Start.AddDays(2));
        var mineEarlier = Owned(Me, "Earlier", Start);
        var joinedLast = JoinedBy(Owned(Other, "Trip", Start.AddDays(-9)), Me, Start.AddDays(5));
        var joinedFirst = JoinedBy(Owned(Other, "House", Start.AddDays(3)), Me, Start.AddDays(4));
        var stranger = Owned(Other, "Not mine", Start);

        var ordered = BudgetOrder.For([joinedLast, mineLater, stranger, joinedFirst, mineEarlier], Me);

        Assert.Equal(["Earlier", "Later", "House", "Trip"], ordered.Select(budget => budget.Name));
    }
}
