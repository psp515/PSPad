using PSPad.Infrastructure.Mongo;
using PSPad.Module.Money.Budgets;
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
