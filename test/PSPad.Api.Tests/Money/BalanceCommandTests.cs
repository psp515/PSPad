using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Api.Tests.Budgeting;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BalanceCommandTests(MongoFixture fixture)
{
    static readonly YearMonth October = new(2026, 10);

    static MoneyValue Pln(decimal amount) => new(amount, "PLN", 1m, October.LastDay);

    static async Task<(HttpClient Client, Guid User, Guid BudgetId)> WithABudgetAsync(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"));
        return (client, user, budgetId);
    }

    static string[] HoldingNames(System.Text.Json.JsonElement snapshot) =>
        snapshot.GetProperty("_holdings").EnumerateArray().Select(holding => holding.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task StartingADeletedMonthRevivesItEmpty()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        await SendAsync(client, ct,
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October),
            new AddHolding(Guid.NewGuid(), user, budgetId, October, Guid.NewGuid(), "Old", HoldingType.Cash, Pln(1m)),
            new DeleteBalanceMonth(Guid.NewGuid(), user, budgetId, October));

        await SendAsync(client, ct, new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October));

        var row = Assert.Single(await SyncedAsync(client, "balancesnapshots", ct));
        Assert.False(row.GetProperty("deleted").GetBoolean());
        Assert.Empty(HoldingNames(row));
    }

    [Fact]
    public async Task AStrangersStartIsRejectedAndNoMonthIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, _, budgetId) = await WithABudgetAsync(factory, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await SignInAsync(stranger, ct);

        var result = Assert.Single(await TrySendAsync(stranger, ct,
            new StartBalanceMonth(Guid.NewGuid(), strangerId, budgetId, October)));

        Assert.False(result.Accepted);
        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.Empty(await SyncedAsync(owner, "balancesnapshots", ct));
    }

    [Fact]
    public async Task RemovingAHoldingDropsItFromTheMonth()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        var wallet = Guid.NewGuid();
        await SendAsync(client, ct,
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October),
            new AddHolding(Guid.NewGuid(), user, budgetId, October, wallet, "Wallet", HoldingType.Cash, Pln(100m)),
            new AddHolding(Guid.NewGuid(), user, budgetId, October, Guid.NewGuid(), "Konto", HoldingType.Savings, Pln(5m)));

        await SendAsync(client, ct, new RemoveHolding(Guid.NewGuid(), user, budgetId, October, wallet));

        Assert.Equal(["Konto"], HoldingNames(Assert.Single(await SyncedAsync(client, "balancesnapshots", ct))));
    }

    [Fact]
    public async Task ALinkedHoldingSurvivesStorageAndAnAccountCannotBeAddedTwice()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        var accountId = Guid.NewGuid();
        await SendAsync(client, ct,
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October),
            new AddHolding(Guid.NewGuid(), user, budgetId, October, Guid.NewGuid(), "Broker", HoldingType.Investment,
                Pln(100m), accountId));

        var results = await TrySendAsync(client, ct,
            new AddHolding(Guid.NewGuid(), user, budgetId, October, Guid.NewGuid(), "Again", HoldingType.Investment,
                Pln(5m), accountId));

        Assert.Equal("That account is already in this month.", Assert.Single(results).Rejection);
        var holding = Assert.Single((await SyncedAsync(client, "balancesnapshots", ct)).Single()
            .GetProperty("_holdings").EnumerateArray());
        Assert.Equal(accountId, holding.GetProperty("investmentAccountId").GetGuid());
    }

    [Fact]
    public async Task HoldingChangesInAMonthWithoutABalanceAreRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        var holdingId = Guid.NewGuid();

        var results = await TrySendAsync(client, ct,
            new AddHolding(Guid.NewGuid(), user, budgetId, October, holdingId, "Konto", HoldingType.Cash, Pln(10m)),
            new EditHolding(Guid.NewGuid(), user, budgetId, October, holdingId, "Cash box", HoldingType.Cash, Pln(10m)),
            new RemoveHolding(Guid.NewGuid(), user, budgetId, October, holdingId));

        Assert.All(results, result =>
        {
            Assert.False(result.Accepted);
            Assert.Equal("That month has no balance yet.", result.Rejection);
        });
        Assert.Empty(await SyncedAsync(client, "balancesnapshots", ct));
    }

    [Fact]
    public async Task AnArchivedBudgetRejectsAHolding()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        await SendAsync(client, ct,
            new StartBalanceMonth(Guid.NewGuid(), user, budgetId, October),
            new ArchiveBudget(Guid.NewGuid(), user, budgetId));

        var result = Assert.Single(await TrySendAsync(client, ct,
            new AddHolding(Guid.NewGuid(), user, budgetId, October, Guid.NewGuid(), "Konto", HoldingType.Cash, Pln(10m))));

        Assert.False(result.Accepted);
        Assert.Equal("This budget is archived.", result.Rejection);
        Assert.Empty(HoldingNames(Assert.Single(await SyncedAsync(client, "balancesnapshots", ct))));
    }

    [Fact]
    public async Task DeletingOrStartingAMonthOfAMissingBudgetIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var missing = Guid.NewGuid();

        var results = await TrySendAsync(client, ct,
            new DeleteBalanceMonth(Guid.NewGuid(), user, missing, October),
            new StartBalanceMonth(Guid.NewGuid(), user, missing, October));

        Assert.All(results, result =>
        {
            Assert.False(result.Accepted);
            Assert.Equal("That budget does not exist.", result.Rejection);
        });
    }
}
