using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Api.Tests.Budgeting;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MoneyEntryCommandTests(MongoFixture fixture)
{
    static readonly DateOnly Day = new(2026, 10, 9);

    static MoneyValue Pln(decimal amount) => new(amount, "PLN", 1m, Day);

    static async Task<(HttpClient Client, Guid User, Guid BudgetId)> WithABudgetAsync(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        await SendAsync(client, ct, new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"));
        return (client, user, budgetId);
    }

    static async Task<string[]> CategoriesAsync(HttpClient client, string field, CancellationToken ct) =>
        Assert.Single(await SyncedAsync(client, "budgets", ct)).GetProperty(field).EnumerateArray()
            .Select(name => name.GetString()!).ToArray();

    [Fact]
    public async Task RecordingUnderAKnownCategoryReusesItsSpelling()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        var before = await CategoriesAsync(client, "_expenseCategories", ct);

        await SendAsync(client, ct,
            new RecordExpense(Guid.NewGuid(), user, Guid.NewGuid(), budgetId, "Lego", "food", Pln(99m), Day, null));

        Assert.Equal("Food", Assert.Single(await SyncedAsync(client, "moneyentries", ct)).GetProperty("category").GetString());
        Assert.Equal(before, await CategoriesAsync(client, "_expenseCategories", ct));
    }

    [Fact]
    public async Task RecordingAnIncomeUnderANewCategoryAddsAnIncomeCategoryOnly()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);

        await SendAsync(client, ct,
            new RecordIncome(Guid.NewGuid(), user, Guid.NewGuid(), budgetId, "Dividend", "Dividends", Pln(50m), Day, null));

        Assert.Contains("Dividends", await CategoriesAsync(client, "_incomeCategories", ct));
        Assert.DoesNotContain("Dividends", await CategoriesAsync(client, "_expenseCategories", ct));
        Assert.Equal("Dividends", Assert.Single(await SyncedAsync(client, "moneyentries", ct)).GetProperty("category").GetString());
    }

    [Fact]
    public async Task EditingIntoANewCategoryAddsIt()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        var entryId = Guid.NewGuid();
        await SendAsync(client, ct,
            new RecordExpense(Guid.NewGuid(), user, entryId, budgetId, "Lego", "Food", Pln(99m), Day, null));

        await SendAsync(client, ct, new EditEntry(Guid.NewGuid(), user, entryId, "Lego", "Hobby", Pln(99m), Day, null));

        Assert.Contains("Hobby", await CategoriesAsync(client, "_expenseCategories", ct));
        Assert.Equal("Hobby", Assert.Single(await SyncedAsync(client, "moneyentries", ct)).GetProperty("category").GetString());
    }

    [Fact]
    public async Task ARecordOnAnArchivedBudgetIsRejectedAndNothingIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        await SendAsync(client, ct, new ArchiveBudget(Guid.NewGuid(), user, budgetId));

        var result = Assert.Single(await TrySendAsync(client, ct,
            new RecordExpense(Guid.NewGuid(), user, Guid.NewGuid(), budgetId, "Lego", "Hobby", Pln(99m), Day, null)));

        Assert.False(result.Accepted);
        Assert.Equal("This budget is archived.", result.Rejection);
        Assert.Empty(await SyncedAsync(client, "moneyentries", ct));
        Assert.DoesNotContain("Hobby", await CategoriesAsync(client, "_expenseCategories", ct));
    }

    [Fact]
    public async Task DeletingAnEntrySyncsItAsDeleted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, budgetId) = await WithABudgetAsync(factory, ct);
        var entryId = Guid.NewGuid();
        await SendAsync(client, ct,
            new RecordExpense(Guid.NewGuid(), user, entryId, budgetId, "Lego", "Food", Pln(99m), Day, null));

        await SendAsync(client, ct, new DeleteEntry(Guid.NewGuid(), user, entryId));

        Assert.True(Assert.Single(await SyncedAsync(client, "moneyentries", ct)).GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task EditingAMissingEntryIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (client, user, _) = await WithABudgetAsync(factory, ct);

        var result = Assert.Single(await TrySendAsync(client, ct,
            new EditEntry(Guid.NewGuid(), user, Guid.NewGuid(), "x", "Food", Pln(1m), Day, null)));

        Assert.False(result.Accepted);
        Assert.Equal("That entry does not exist.", result.Rejection);
    }
}
