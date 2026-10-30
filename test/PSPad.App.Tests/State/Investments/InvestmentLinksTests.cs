using PSPad.App.State.Investments;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinksTests
{
    static readonly Guid Account = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void TheFirstTabNeedsNoSegment()
    {
        Assert.Equal("/investments", InvestmentLinks.For(null, InvestmentTabs.Positions));
        Assert.Equal($"/investments/{Account}", InvestmentLinks.For(Account, InvestmentTabs.Positions));
    }

    [Fact]
    public void OtherTabsFollowTheAccount()
    {
        Assert.Equal("/investments/operations", InvestmentLinks.For(null, InvestmentTabs.Operations));
        Assert.Equal($"/investments/{Account}/accounts", InvestmentLinks.For(Account, InvestmentTabs.Accounts));
    }

    [Theory]
    [InlineData(null, InvestmentTabs.Positions)]
    [InlineData("operations", InvestmentTabs.Operations)]
    [InlineData("ACCOUNTS", InvestmentTabs.Accounts)]
    [InlineData("nonsense", InvestmentTabs.Positions)]
    public void ATabNameReadsBackToItsTab(string? name, int tab) => Assert.Equal(tab, InvestmentTabs.IndexOf(name));

    [Fact]
    public void TheImportLinkCarriesTheAccount()
    {
        Assert.Equal($"/investments/import?account={Account}", InvestmentLinks.ForImport(Account));
        Assert.Equal("/investments/import", InvestmentLinks.ForImport(null));
    }

    [Fact]
    public void AnOpenOperationReadsBackFromTheAddress()
    {
        var operation = Guid.NewGuid();
        var uri = InvestmentLinks.ForOperation("https://pspad.test/investments?x=1", operation);

        Assert.Equal(operation, InvestmentLinks.OperationFrom(uri));
        Assert.False(InvestmentLinks.IsNewOperation(uri));
        Assert.True(InvestmentLinks.IsNewOperation(InvestmentLinks.ForNewOperation(uri)));
        Assert.Null(InvestmentLinks.OperationFrom(InvestmentLinks.ForNewOperation(uri)));
    }

    [Fact]
    public void AnOpenAccountReadsBackFromTheAddress()
    {
        var uri = InvestmentLinks.ForAccount("https://pspad.test/investments", Account);

        Assert.Equal(Account, InvestmentLinks.AccountFrom(uri));
        Assert.True(InvestmentLinks.IsNewAccount(InvestmentLinks.ForNewAccount(uri)));
    }

    [Fact]
    public void AnAccountPanelDoesNotOpenOnTheImportPage() =>
        Assert.Null(InvestmentLinks.AccountFrom($"https://pspad.test{InvestmentLinks.ForImport(Account)}"));

    [Theory]
    [InlineData("https://pspad.test/investments/22222222-2222-2222-2222-222222222222/operations?investmentoperation=new", true)]
    [InlineData("https://pspad.test/investments/22222222-2222-2222-2222-222222222222", true)]
    [InlineData("https://pspad.test/investments/operations", false)]
    [InlineData("https://pspad.test/budgets/22222222-2222-2222-2222-222222222222", false)]
    public void TheAccountInTheAddressIsTheDefaultForANewOperation(string uri, bool found) =>
        Assert.Equal(found ? Account : null, InvestmentLinks.AccountInPath(uri));
}
