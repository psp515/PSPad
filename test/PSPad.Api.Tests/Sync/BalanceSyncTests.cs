using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BalanceSyncTests(MongoFixture fixture)
{
    static readonly YearMonth September = new(2026, 9);
    static readonly YearMonth October = new(2026, 10);

    static async Task<(HttpClient Client, Guid User)> SignInAsync(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        return (client, await Sharing.SignInAsync(client, ct));
    }

    [Fact]
    public async Task AStartedMonthCarriesItsHoldingsForwardAndSyncs()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        var holdingId = Guid.NewGuid();

        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, September),
            new AddHolding(Guid.NewGuid(), user, budgetId, September, holdingId, "Konto", HoldingType.Savings,
                new Money(10.5m, "EUR", 4.2512m, new DateOnly(2026, 9, 30))),
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var rows = sync!.Documents["balancesnapshots"];
        Assert.Equal(2, rows.Length);
        var october = Assert.Single(rows, row => row.GetProperty("_month").GetString() == "2026-10");
        Assert.Equal(BalanceSnapshot.IdFor(budgetId, October), october.GetProperty("id").GetGuid());
        Assert.Equal(budgetId, october.GetProperty("budgetId").GetGuid());
        Assert.Equal(user, october.GetProperty("userId").GetGuid());
        Assert.False(october.TryGetProperty("month", out _));
        var holding = Assert.Single(october.GetProperty("_holdings").EnumerateArray().ToArray());
        Assert.Equal(holdingId, holding.GetProperty("holdingId").GetGuid());
        Assert.Equal("Konto", holding.GetProperty("name").GetString());
        Assert.Equal((int)HoldingType.Savings, holding.GetProperty("type").GetInt32());
        var money = holding.GetProperty("money");
        Assert.Equal(10.5m, money.GetProperty("amount").GetDecimal());
        Assert.Equal(4.2512m, money.GetProperty("rateToPln").GetDecimal());
        Assert.Equal("2026-09-30", money.GetProperty("rateDate").GetString());
        Assert.False(money.TryGetProperty("inPln", out _));
    }

    [Fact]
    public async Task AHoldingEditArrivesInADeltaPullWithoutTheUnchangedBudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        var holdingId = Guid.NewGuid();
        var money = new Money(100m, "PLN", 1m, October.LastDay);
        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October),
            new AddHolding(Guid.NewGuid(), user, budgetId, October, holdingId, "Wallet", HoldingType.Cash, money));
        var first = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        await Sharing.SendAsync(client, ct,
            new EditHolding(Guid.NewGuid(), user, budgetId, October, holdingId, "Cash box", HoldingType.Cash, money));
        var delta = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}", ct);

        var row = Assert.Single(delta!.Documents["balancesnapshots"]);
        Assert.Equal("Cash box", Assert.Single(row.GetProperty("_holdings").EnumerateArray().ToArray())
            .GetProperty("name").GetString());
        Assert.Empty(delta.Documents["budgets"]);
    }

    [Fact]
    public async Task ADeletedMonthSyncsAsDeleted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        await Sharing.SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October),
            new DeleteBalanceMonth(Guid.NewGuid(), user, budgetId, October));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.True(Assert.Single(sync!.Documents["balancesnapshots"]).GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task AnotherUsersBalancesNeverSync()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignInAsync(factory, ct);
        var budgetId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateBudget(Guid.NewGuid(), ownerId, budgetId, "Mine"),
            new StartBalanceMonth(Guid.NewGuid(), ownerId, budgetId, October));

        var (other, _) = await SignInAsync(factory, ct);
        var sync = await other.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Documents["balancesnapshots"]);
    }
}
