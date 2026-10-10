using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Tests.Fakes;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Entries;

[UnitTest]
public class MoneyEntryHandlerTests
{
    readonly FakeDocumentStore<Budget> _budgets = new();
    readonly FakeDocumentStore<MoneyEntry> _entries = new();
    readonly FakeUnitOfWork _work = new();
    readonly FixedClock _clock = new(Now);

    Budget Seeded(Budget budget)
    {
        _budgets.Seed(budget);
        return budget;
    }

    RecordExpense Expense(Budget budget, string category) =>
        new(Guid.NewGuid(), Owner, Guid.NewGuid(), budget.Id, "Lego", category, new Money(99m, "PLN", 1m, Today), Today, null);

    [Fact]
    public async Task RecordingUnderANewCategoryAddsItInTheSameUnitOfWork()
    {
        var budget = Seeded(ABudget());
        var command = Expense(budget, "Hobby");

        var result = await new RecordExpenseHandler(_budgets, _entries, _work, _clock).HandleAsync(command, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        Assert.Equal(2, _work.Staged.Count);
        var added = Assert.IsType<CategoryAdded>(Assert.Single(_work.Staged[0].Events));
        Assert.Equal((CategoryKind.Expense, "Hobby"), (added.Kind, added.Name));
        Assert.Same(budget, _work.Staged[0].Aggregate);
        Assert.Contains("Hobby", budget.ExpenseCategories);
        var recorded = Assert.IsType<ExpenseRecorded>(Assert.Single(_work.Staged[1].Events));
        Assert.Equal(command.EntryId, recorded.AggregateId);
        Assert.Equal("Hobby", recorded.Category);
    }

    [Fact]
    public async Task RecordingUnderAKnownCategoryStagesOnlyTheEntry()
    {
        var budget = Seeded(ABudget());

        await new RecordExpenseHandler(_budgets, _entries, _work, _clock).HandleAsync(Expense(budget, "food"), CancellationToken.None);

        var staged = Assert.Single(_work.Staged);
        Assert.Equal("Food", Assert.IsType<ExpenseRecorded>(Assert.Single(staged.Events)).Category);
    }

    [Fact]
    public async Task RecordingAnIncomeUnderANewCategoryAddsAnIncomeCategory()
    {
        var budget = Seeded(ABudget());

        await new RecordIncomeHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new RecordIncome(Guid.NewGuid(), Owner, Guid.NewGuid(), budget.Id, "Dividend", "Dividends",
                new Money(50m, "PLN", 1m, Today), Today, null), CancellationToken.None);

        Assert.Contains("Dividends", budget.IncomeCategories);
        Assert.DoesNotContain("Dividends", budget.ExpenseCategories);
        Assert.IsType<IncomeRecorded>(Assert.Single(_work.Staged[1].Events));
    }

    [Fact]
    public async Task EditingIntoANewCategoryAddsIt()
    {
        var budget = Seeded(ABudget());
        var entry = AnExpense(budget);
        _entries.Seed(entry);

        var result = await new EditEntryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new EditEntry(Guid.NewGuid(), Owner, entry.Id, entry.Name, "Hobby", entry.Money, entry.Date, null),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<CategoryAdded>(Assert.Single(_work.Staged[0].Events));
        Assert.IsType<MoneyEntryEdited>(Assert.Single(_work.Staged[1].Events));
        Assert.Equal("Hobby", entry.Category);
    }

    [Fact]
    public async Task ARejectedRecordCommitsNothing()
    {
        var budget = Seeded(Archive(ABudget()));

        var result = await new RecordExpenseHandler(_budgets, _entries, _work, _clock).HandleAsync(Expense(budget, "Hobby"), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("This budget is archived.", result.Rejection);
        Assert.False(_work.Committed);
        Assert.Empty(_work.Staged);
        Assert.DoesNotContain("Hobby", budget.ExpenseCategories);
    }

    [Fact]
    public async Task DeletingStagesTheSoftDelete()
    {
        var budget = Seeded(ABudget());
        var entry = AnExpense(budget);
        _entries.Seed(entry);

        var result = await new DeleteEntryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new DeleteEntry(Guid.NewGuid(), Owner, entry.Id), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<MoneyEntryDeleted>(Assert.Single(Assert.Single(_work.Staged).Events));
        Assert.True(entry.Deleted);
    }

    [Fact]
    public async Task EditingAMissingEntryIsRejected()
    {
        var result = await new EditEntryHandler(_budgets, _entries, _work, _clock).HandleAsync(
            new EditEntry(Guid.NewGuid(), Owner, Guid.NewGuid(), "x", "Food", new Money(1m, "PLN", 1m, Today), Today, null),
            CancellationToken.None);

        Assert.Equal("That entry does not exist.", result.Rejection);
        Assert.False(_work.Committed);
    }
}
