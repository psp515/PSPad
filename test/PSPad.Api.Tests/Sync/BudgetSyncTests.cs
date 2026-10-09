using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Preferences;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BudgetSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task ABudgetAndItsAddedCategorySyncAsOneRow()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var budgetId = Guid.NewGuid();

        await Send(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new AddCategory(Guid.NewGuid(), user, budgetId, CategoryKind.Expense, "Hobby"));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var row = Assert.Single(sync!.Documents["budgets"]);
        Assert.Equal(budgetId, row.GetProperty("id").GetGuid());
        Assert.Equal("Personal", row.GetProperty("name").GetString());
        Assert.Equal("Hobby", row.GetProperty("_expenseCategories").EnumerateArray().Last().GetString());
    }

    [Fact]
    public async Task TheDefaultCurrencySyncs()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

        await Send(client, ct, new SetDefaultCurrency(Guid.NewGuid(), user, "EUR"));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var row = Assert.Single(sync!.Documents["moneypreferences"]);
        Assert.Equal(MoneyPreferences.IdFor(user), row.GetProperty("id").GetGuid());
        Assert.Equal("EUR", row.GetProperty("defaultCurrency").GetString());
    }

    [Fact]
    public async Task AnotherUsersBudgetNeverSyncs()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = (await owner.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        await Send(owner, ct, new CreateBudget(Guid.NewGuid(), ownerId, Guid.NewGuid(), "Mine"));

        var other = factory.ClientFor(Guid.NewGuid().ToString());
        var sync = await other.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Documents["budgets"]);
    }

    static async Task Send(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.All(results!, result => Assert.True(result.Accepted, result.Rejection));
    }
}
