using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class BudgetHandlerTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    readonly FakeDocumentStore<Budget> _budgets = new();
    readonly FakeUnitOfWork _work = new();
    readonly FixedClock _clock = new(Now);

    [Fact]
    public async Task CreatingStagesAndCommitsTheBudget()
    {
        var id = Guid.NewGuid();

        var result = await new CreateBudgetHandler(_budgets, _work, _clock).HandleAsync(
            new CreateBudget(Guid.NewGuid(), Owner, id, "Personal"), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        var staged = Assert.Single(_work.Staged);
        Assert.Equal(id, staged.Aggregate.Id);
        Assert.IsType<BudgetCreated>(Assert.Single(staged.Events));
    }

    [Fact]
    public async Task AddingToAMissingBudgetIsRejectedAndNothingCommits()
    {
        var result = await new AddCategoryHandler(_budgets, _work, _clock).HandleAsync(
            new AddCategory(Guid.NewGuid(), Owner, Guid.NewGuid(), CategoryKind.Expense, "Hobby"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task ArchivingAnExistingBudgetCommits()
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), Owner, Guid.NewGuid(), "Personal"), Now));
        _budgets.Seed(budget);

        var result = await new ArchiveBudgetHandler(_budgets, _work, _clock).HandleAsync(
            new ArchiveBudget(Guid.NewGuid(), Owner, budget.Id), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<BudgetArchived>(Assert.Single(_work.Events));
    }
}
