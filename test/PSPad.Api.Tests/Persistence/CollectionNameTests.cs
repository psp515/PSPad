using PSPad.Infrastructure.Mongo;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Preferences;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;
using InboxAggregate = PSPad.Module.Tasks.Inbox.Inbox;

namespace PSPad.Api.Tests.Persistence;

[UnitTest]
public class CollectionNameTests
{
    [Fact]
    public void ExistingAggregatesKeepTheirCollections()
    {
        Assert.Equal("areas", MongoContext.NameOf(typeof(Area)));
        Assert.Equal("inboxes", MongoContext.NameOf(typeof(InboxAggregate)));
        Assert.Equal("referenceitems", MongoContext.NameOf(typeof(ReferenceItem)));
    }

    [Fact]
    public void MoneyAggregatesGetPlainEnglishPlurals()
    {
        Assert.Equal("budgets", MongoContext.NameOf(typeof(Budget)));
        Assert.Equal("moneypreferences", MongoContext.NameOf(typeof(MoneyPreferences)));
        Assert.Equal("moneyentries", MongoContext.NameOf(typeof(MoneyEntry)));
        Assert.Equal("balancesnapshots", MongoContext.NameOf(typeof(BalanceSnapshot)));
    }

    [Fact]
    public void InvestmentAggregatesGetPlainEnglishPlurals()
    {
        Assert.Equal("investmentportfolios", MongoContext.NameOf(typeof(InvestmentPortfolio)));
        Assert.Equal("investmentaccounts", MongoContext.NameOf(typeof(InvestmentAccount)));
        Assert.Equal("investmentoperations", MongoContext.NameOf(typeof(InvestmentOperation)));
        Assert.Equal("instruments", MongoContext.NameOf(typeof(Instrument)));
    }

    [Theory]
    [InlineData("MoneyEntry", "moneyentries")]
    [InlineData("Category", "categories")]
    [InlineData("Day", "days")]
    public void NamesEndingInYPluraliseLikeEnglish(string type, string expected)
    {
        Assert.Equal(expected, MongoContext.Pluralise(type.ToLowerInvariant()));
    }
}
