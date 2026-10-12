using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Tests.Fakes;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class CategoryHandlerTests
{
    readonly FakeDocumentStore<Budget> _budgets = new();
    readonly FakeDocumentStore<MoneyEntry> _entries = new();
    readonly FakeUnitOfWork _work = new();
    readonly FixedClock _clock = new(Now);

    [Fact]
    public async Task RenamingStagesTheBudgetAndEachRelabelledEntryInOneCommit()
    {
        var budget = ABudget();
        _budgets.Seed(budget);
        _entries.Seed(AnExpense(budget, "Food"));
        _entries.Seed(AnExpense(budget, "Food"));

        var result = await new RenameCategoryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new RenameCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Food", "Groceries"),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.Equal(3, _work.Staged.Count);
    }

    [Fact]
    public async Task MergingCommits()
    {
        var budget = ABudget();
        _budgets.Seed(budget);

        var result = await new MergeCategoryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new MergeCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Bike", "Car"), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<CategoriesMerged>(Assert.Single(_work.Events));
    }

    [Fact]
    public async Task RemovingAUsedCategoryIsRejectedAndNothingCommits()
    {
        var budget = ABudget();
        _budgets.Seed(budget);
        _entries.Seed(AnExpense(budget, "PC"));

        var result = await new RemoveCategoryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new RemoveCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "PC"), CancellationToken.None);

        Assert.Equal("That category still has entries.", result.Rejection);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task RenamingInAMissingBudgetIsRejected()
    {
        var result = await new RenameCategoryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new RenameCategory(Guid.NewGuid(), Owner, Guid.NewGuid(), CategoryKind.Expense, "Food", "Groceries"),
            CancellationToken.None);

        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.False(_work.Committed);
    }
}
