using System.Text.Json;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Api.Tests.Budgeting;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ReturnCommandTests(MongoFixture fixture)
{
    static readonly DateOnly Day = new(2026, 10, 9);

    static MoneyValue Eur(decimal amount) => new(amount, "EUR", 4.268m, Day);

    static MoneyValue Pln(decimal amount) => new(amount, "PLN", 1m, Day);

    static async Task<(HttpClient Client, Guid User, Guid PurchaseId)> WithAPurchaseAsync(
        ApiFactory factory, MoneyValue price, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"));
        await SendAsync(client, ct,
            new RecordExpense(Guid.NewGuid(), user, purchaseId, budgetId, "Lego", "Food", price, Day, null));
        return (client, user, purchaseId);
    }

    static async Task<JsonElement> EntryAsync(HttpClient client, Guid id, CancellationToken ct) =>
        (await SyncedAsync(client, "moneyentries", ct)).Single(entry => entry.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task ARecordedReturnSyncsLinkedToItsPurchase()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, purchaseId) = await WithAPurchaseAsync(factory, Eur(120m), ct);
        var returnId = Guid.NewGuid();

        await SendAsync(client, ct, new RecordReturn(Guid.NewGuid(), user, returnId, purchaseId, 50m, Day, null));

        Assert.Equal(2, (await SyncedAsync(client, "moneyentries", ct)).Count);
        var returned = await EntryAsync(client, returnId, ct);
        Assert.Equal(purchaseId, returned.GetProperty("returnOf").GetGuid());
        Assert.Equal("Lego", returned.GetProperty("name").GetString());
        Assert.Equal("Food", returned.GetProperty("category").GetString());
        Assert.Equal(50m, returned.GetProperty("money").GetProperty("amount").GetDecimal());
        Assert.Equal(4.268m, returned.GetProperty("money").GetProperty("rateToPln").GetDecimal());
    }

    [Fact]
    public async Task ReturningMoreThanIsLeftIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, purchaseId) = await WithAPurchaseAsync(factory, Pln(100m), ct);
        await SendAsync(client, ct, new RecordReturn(Guid.NewGuid(), user, Guid.NewGuid(), purchaseId, 60m, Day, null));

        var result = Assert.Single(await TrySendAsync(client, ct,
            new RecordReturn(Guid.NewGuid(), user, Guid.NewGuid(), purchaseId, 41m, Day, null)));

        Assert.False(result.Accepted);
        Assert.Equal("That is more than is left to return.", result.Rejection);
        await SendAsync(client, ct, new RecordReturn(Guid.NewGuid(), user, Guid.NewGuid(), purchaseId, 40m, Day, null));
    }

    [Fact]
    public async Task EditingAReturnUpdatesIt()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, purchaseId) = await WithAPurchaseAsync(factory, Pln(100m), ct);
        var returnId = Guid.NewGuid();
        await SendAsync(client, ct, new RecordReturn(Guid.NewGuid(), user, returnId, purchaseId, 50m, Day, null));

        await SendAsync(client, ct, new EditReturn(Guid.NewGuid(), user, returnId, 30m, Day, null));

        var returned = await EntryAsync(client, returnId, ct);
        Assert.Equal(30m, returned.GetProperty("money").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task DeletingThePurchaseDeletesItsReturns()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, purchaseId) = await WithAPurchaseAsync(factory, Pln(100m), ct);
        var returnId = Guid.NewGuid();
        await SendAsync(client, ct, new RecordReturn(Guid.NewGuid(), user, returnId, purchaseId, 50m, Day, null));

        await SendAsync(client, ct, new DeleteEntry(Guid.NewGuid(), user, purchaseId));

        Assert.True((await EntryAsync(client, purchaseId, ct)).GetProperty("deleted").GetBoolean());
        Assert.True((await EntryAsync(client, returnId, ct)).GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task RecategorisingThePurchaseMovesItsReturns()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, purchaseId) = await WithAPurchaseAsync(factory, Pln(100m), ct);
        var returnId = Guid.NewGuid();
        await SendAsync(client, ct, new RecordReturn(Guid.NewGuid(), user, returnId, purchaseId, 50m, Day, null));

        await SendAsync(client, ct, new EditEntry(Guid.NewGuid(), user, purchaseId, "Lego", "Hobby", Pln(100m), Day, null));

        Assert.Equal("Hobby", (await EntryAsync(client, returnId, ct)).GetProperty("category").GetString());
    }
}
