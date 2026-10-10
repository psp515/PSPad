using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MoneyEntrySyncTests(MongoFixture fixture)
{
    static readonly DateOnly Day = new(2026, 10, 9);

    static async Task<(HttpClient Client, Guid User)> SignInAsync(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        return (client, await Sharing.SignInAsync(client, ct));
    }

    [Fact]
    public async Task AnEntryAndItsAutoAddedCategorySync()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new RecordExpense(Guid.NewGuid(), user, entryId, budgetId, "Lego", "Hobby",
                new Money(12.5m, "EUR", 4.2512m, Day), Day, "birthday"));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var row = Assert.Single(sync!.Documents["moneyentries"]);
        Assert.Equal(entryId, row.GetProperty("id").GetGuid());
        Assert.Equal(budgetId, row.GetProperty("budgetId").GetGuid());
        Assert.Equal("Hobby", row.GetProperty("category").GetString());
        Assert.Equal("birthday", row.GetProperty("note").GetString());
        var money = row.GetProperty("money");
        Assert.Equal(12.5m, money.GetProperty("amount").GetDecimal());
        Assert.Equal("EUR", money.GetProperty("currency").GetString());
        Assert.Equal(4.2512m, money.GetProperty("rateToPln").GetDecimal());
        Assert.Equal("2026-10-09", money.GetProperty("rateDate").GetString());
        Assert.False(money.TryGetProperty("inPln", out _));
        var budgetRow = Assert.Single(sync.Documents["budgets"]);
        Assert.Equal("Hobby", budgetRow.GetProperty("_expenseCategories").EnumerateArray().Last().GetString());
    }

    [Fact]
    public async Task AnEditArrivesInADeltaPullWithoutTheUnchangedBudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new RecordExpense(Guid.NewGuid(), user, entryId, budgetId, "Rolls", "Food",
                new Money(3m, "PLN", 1m, Day), Day, null));
        var first = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        await Sharing.SendAsync(client, ct,
            new EditEntry(Guid.NewGuid(), user, entryId, "Bread", "Food", new Money(4m, "PLN", 1m, Day), Day, null));
        var delta = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}", ct);

        var row = Assert.Single(delta!.Documents["moneyentries"]);
        Assert.Equal("Bread", row.GetProperty("name").GetString());
        Assert.Empty(delta.Documents["budgets"]);
    }

    [Fact]
    public async Task RenamingACategoryRelabelsTheStoredEntries()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new RecordExpense(Guid.NewGuid(), user, Guid.NewGuid(), budgetId, "Rolls", "Food",
                new Money(3m, "PLN", 1m, Day), Day, null),
            new RenameCategory(Guid.NewGuid(), user, budgetId, CategoryKind.Expense, "Food", "Groceries"));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Equal("Groceries", Assert.Single(sync!.Documents["moneyentries"]).GetProperty("category").GetString());
        var categories = Assert.Single(sync.Documents["budgets"]).GetProperty("_expenseCategories")
            .EnumerateArray().Select(name => name.GetString()).ToArray();
        Assert.Contains("Groceries", categories);
        Assert.DoesNotContain("Food", categories);
    }

    [Fact]
    public async Task AnotherUsersEntriesNeverSync()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateBudget(Guid.NewGuid(), ownerId, budgetId, "Mine"),
            new RecordExpense(Guid.NewGuid(), ownerId, Guid.NewGuid(), budgetId, "Rolls", "Food",
                new Money(3m, "PLN", 1m, Day), Day, null));

        var (other, _) = await SignInAsync(factory, ct);
        var sync = await other.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Documents["moneyentries"]);
    }
}
