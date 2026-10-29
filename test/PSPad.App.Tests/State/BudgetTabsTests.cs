using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class BudgetTabsTests
{
    [Theory]
    [InlineData(null, BudgetTabs.Overview)]
    [InlineData("overview", BudgetTabs.Overview)]
    [InlineData("entries", BudgetTabs.Entries)]
    [InlineData("Balance", BudgetTabs.Balance)]
    [InlineData("stats", BudgetTabs.Stats)]
    [InlineData("settings", BudgetTabs.Settings)]
    public void EachTabNameOpensItsView(string? tab, int view) =>
        Assert.Equal(view, BudgetTabs.IndexOf(tab));

    [Theory]
    [InlineData("month", BudgetTabs.Entries)]
    [InlineData("summary", BudgetTabs.Stats)]
    public void OldTabNamesStillOpenTheirView(string old, int view) =>
        Assert.Equal(view, BudgetTabs.IndexOf(old));

    [Fact]
    public void AnUnknownTabIsTheOverview() =>
        Assert.Equal(BudgetTabs.Overview, BudgetTabs.IndexOf("nonsense"));

    [Fact]
    public void AViewIsAddressedByItsName() =>
        Assert.Equal("stats", BudgetTabs.NameOf(BudgetTabs.Stats));
}
