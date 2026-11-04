using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;
using PSPad.App.State.Investments;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkStateTests
{
    static readonly YearMonth September = new(2026, 9);
    static readonly YearMonth October = new(2026, 10);

    [Fact]
    public void TheMonthsOwnLinksAreUsedWhenItExists()
    {
        var budget = Given.ABudget();
        var account = Given.AnAccount();
        var holding = Given.Linked(account);

        var state = InvestmentLinkState.For([Given.ASnapshot(budget, October, holding)], October);

        Assert.True(state.MonthExists);
        Assert.Equal(holding.HoldingId, state.Links[account.Id].HoldingId);
    }

    [Fact]
    public void AMonthWithoutASnapshotSeesTheLinksItWillCarryFromTheLatestEarlierOne()
    {
        var budget = Given.ABudget();
        var account = Given.AnAccount();
        var older = Given.ASnapshot(budget, new YearMonth(2026, 8), Given.Linked(account, "old"));
        var carried = Given.Linked(account, "carried");
        var latest = Given.ASnapshot(budget, September, carried);

        var state = InvestmentLinkState.For([older, latest], October);

        Assert.False(state.MonthExists);
        Assert.Equal(carried.HoldingId, state.Links[account.Id].HoldingId);
    }

    [Fact]
    public void ALaterMonthIsNotCarried()
    {
        var budget = Given.ABudget();
        var account = Given.AnAccount();
        var later = Given.ASnapshot(budget, new YearMonth(2026, 11), Given.Linked(account));

        Assert.Empty(InvestmentLinkState.For([later], October).Links);
    }

    [Fact]
    public void HoldingsWithoutALinkAreIgnored()
    {
        var budget = Given.ABudget();
        var plain = Given.Linked(Given.AnAccount()) with { InvestmentAccountId = null };

        Assert.Empty(InvestmentLinkState.For([Given.ASnapshot(budget, October, plain)], October).Links);
    }
}
