using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Api.Tests.Budgeting;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CategoryCommandTests(MongoFixture fixture)
{
    static readonly DateOnly Day = new(2026, 10, 9);

    static RecordExpense Expense(Guid user, Guid budgetId, Guid entryId, string category) =>
        new(Guid.NewGuid(), user, entryId, budgetId, "Item", category, new MoneyValue(5m, "PLN", 1m, Day), Day, null);

    static string[] Names(System.Text.Json.JsonElement budget, string field) =>
        budget.GetProperty(field).EnumerateArray().Select(name => name.GetString()!).ToArray();

    [Fact]
    public async Task RenamingOnlyRelabelsEntriesOfThatKindInThatBudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var lunch = Guid.NewGuid();
        var income = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            new CreateBudget(Guid.NewGuid(), user, otherId, "Other"),
            Expense(user, budgetId, lunch, "Food"),
            Expense(user, budgetId, deleted, "Food"),
            new DeleteEntry(Guid.NewGuid(), user, deleted),
            new RecordIncome(Guid.NewGuid(), user, income, budgetId, "Pay", "Food", new MoneyValue(5m, "PLN", 1m, Day), Day, null),
            Expense(user, otherId, elsewhere, "Food"));

        await SendAsync(client, ct,
            new RenameCategory(Guid.NewGuid(), user, budgetId, CategoryKind.Expense, "Food", "Groceries"));

        var entries = (await SyncedAsync(client, "moneyentries", ct))
            .ToDictionary(row => row.GetProperty("id").GetGuid(), row => row.GetProperty("category").GetString());
        Assert.Equal("Groceries", entries[lunch]);
        Assert.Equal("Food", entries[deleted]);
        Assert.Equal("Food", entries[income]);
        Assert.Equal("Food", entries[elsewhere]);
    }

    [Fact]
    public async Task MergingMovesEntriesIntoTheTargetAndDropsTheSource()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        var ride = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            Expense(user, budgetId, ride, "Bike"));

        await SendAsync(client, ct,
            new MergeCategory(Guid.NewGuid(), user, budgetId, CategoryKind.Expense, "Bike", "Car"));

        Assert.Equal("Car", Assert.Single(await SyncedAsync(client, "moneyentries", ct)).GetProperty("category").GetString());
        var categories = Names(Assert.Single(await SyncedAsync(client, "budgets", ct)), "_expenseCategories");
        Assert.DoesNotContain("Bike", categories);
        Assert.Contains("Car", categories);
    }

    [Fact]
    public async Task RemovingAUsedCategoryIsRejectedAndTheBudgetKeepsIt()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            Expense(user, budgetId, Guid.NewGuid(), "PC"));

        var result = Assert.Single(await TrySendAsync(client, ct,
            new RemoveCategory(Guid.NewGuid(), user, budgetId, CategoryKind.Expense, "PC")));

        Assert.False(result.Accepted);
        Assert.Equal("That category still has entries.", result.Rejection);
        Assert.Contains("PC", Names(Assert.Single(await SyncedAsync(client, "budgets", ct)), "_expenseCategories"));
    }

    [Fact]
    public async Task RemovingIsAllowedOnceItsEntriesAreDeleted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var budgetId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateBudget(Guid.NewGuid(), user, budgetId, "Personal"),
            Expense(user, budgetId, entryId, "Gifts"),
            new DeleteEntry(Guid.NewGuid(), user, entryId));

        await SendAsync(client, ct, new RemoveCategory(Guid.NewGuid(), user, budgetId, CategoryKind.Expense, "Gifts"));

        var budget = Assert.Single(await SyncedAsync(client, "budgets", ct));
        Assert.DoesNotContain("Gifts", Names(budget, "_expenseCategories"));
        Assert.Contains("Gifts", Names(budget, "_incomeCategories"));
    }

    [Fact]
    public async Task RenamingInAMissingBudgetIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);

        var result = Assert.Single(await TrySendAsync(client, ct,
            new RenameCategory(Guid.NewGuid(), user, Guid.NewGuid(), CategoryKind.Expense, "Food", "Groceries")));

        Assert.False(result.Accepted);
        Assert.Equal("That budget does not exist.", result.Rejection);
    }
}
