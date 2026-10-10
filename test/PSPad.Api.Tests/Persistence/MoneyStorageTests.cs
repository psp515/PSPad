using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MoneyStorageTests(MongoFixture fixture)
{
    [Fact]
    public async Task AmountsAndRatesAreStoredAsDecimal128AndInPlnIsNotStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await Sharing.SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        var day = new DateOnly(2026, 10, 9);

        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new RecordExpense(Guid.NewGuid(), user, Guid.NewGuid(), budgetId, "Train", "Travel",
                new Money(12.35m, "EUR", 4.2512m, day), day, null));

        var raw = await TestContext.For(fixture).Collection<BsonDocument>("moneyentries")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", user))
            .SingleAsync(ct);

        var money = raw["money"].AsBsonDocument;
        Assert.Equal(BsonType.Decimal128, money["amount"].BsonType);
        Assert.Equal(BsonType.Decimal128, money["rateToPln"].BsonType);
        Assert.Equal(12.35m, money["amount"].AsDecimal);
        Assert.Equal(4.2512m, money["rateToPln"].AsDecimal);
        Assert.Equal("EUR", money["currency"].AsString);
        Assert.False(money.Contains("inPln"));
    }
}
