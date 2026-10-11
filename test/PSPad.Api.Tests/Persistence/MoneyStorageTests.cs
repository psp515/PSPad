using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
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

    [Fact]
    public async Task HoldingsAreStoredAsDecimal128AndTheMonthAsText()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await Sharing.SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        var october = new YearMonth(2026, 10);

        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, october),
            new AddHolding(Guid.NewGuid(), user, budgetId, october, Guid.NewGuid(), "Konto", HoldingType.Savings,
                new Money(12.35m, "EUR", 4.2512m, october.LastDay)));

        var raw = await TestContext.For(fixture).Collection<BsonDocument>("balancesnapshots")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", user))
            .SingleAsync(ct);

        Assert.Equal("2026-10", raw["_month"].AsString);
        Assert.False(raw.Contains("month"));
        Assert.False(raw.Contains("holdings"));
        var money = raw["_holdings"].AsBsonArray.Single().AsBsonDocument["money"].AsBsonDocument;
        Assert.Equal(BsonType.Decimal128, money["amount"].BsonType);
        Assert.Equal(BsonType.Decimal128, money["rateToPln"].BsonType);
        Assert.Equal(12.35m, money["amount"].AsDecimal);
        Assert.False(money.Contains("inPln"));
    }
}
