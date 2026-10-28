using System.Text.Json;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Budgeting;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BudgetCommandTests(MongoFixture fixture)
{
    [Fact]
    public async Task AddingACategoryToAMissingBudgetIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var owner = await SignInAsync(client, ct);

        var result = Assert.Single(await TrySendAsync(client, ct,
            new AddCategory(Guid.NewGuid(), owner, Guid.NewGuid(), CategoryKind.Expense, "Hobby")));

        Assert.False(result.Accepted);
        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.Empty(await SyncedAsync(client, "budgets", ct));
    }

    [Fact]
    public async Task ArchivingABudgetSyncsItAsArchived()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var owner = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateBudget(Guid.NewGuid(), owner, budgetId, "Personal"));

        await SendAsync(client, ct, new ArchiveBudget(Guid.NewGuid(), owner, budgetId));

        var row = Assert.Single(await SyncedAsync(client, "budgets", ct));
        Assert.Equal(JsonValueKind.String, row.GetProperty("archivedAt").ValueKind);
    }

    [Fact]
    public async Task AStrangerCannotArchiveABudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await SignInAsync(owner, ct);
        var budgetId = Guid.NewGuid();
        await SendAsync(owner, ct, new CreateBudget(Guid.NewGuid(), ownerId, budgetId, "Personal"));
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await SignInAsync(stranger, ct);

        var result = Assert.Single(await TrySendAsync(stranger, ct, new ArchiveBudget(Guid.NewGuid(), strangerId, budgetId)));

        Assert.False(result.Accepted);
        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.Equal(JsonValueKind.Null, Assert.Single(await SyncedAsync(owner, "budgets", ct)).GetProperty("archivedAt").ValueKind);
    }
}
