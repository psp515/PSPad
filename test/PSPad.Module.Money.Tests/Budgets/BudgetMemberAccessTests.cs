using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class BudgetMemberAccessTests
{
    static readonly Guid Member = Given.Member;
    static readonly DateTimeOffset Now = Given.Now;

    static Budget SharedWithMember() => Given.Join(Given.Share(Given.ABudget()));

    static Money Pln(decimal amount) => new(amount, "PLN", 1m, Given.Today);

    [Fact]
    public void SomeoneWhoIsNotAMemberIsRejectedForGood() =>
        Assert.True(Assert.Throws<DomainRejectedException>(
            () => BudgetAccess.To(SharedWithMember(), Given.Stranger)).Unrecoverable);

    [Fact]
    public void AMissingBudgetIsRejectedForGood() =>
        Assert.True(Assert.Throws<DomainRejectedException>(() => BudgetAccess.To(null, Member)).Unrecoverable);

    [Fact]
    public void AMemberTryingAnOwnerCommandIsRejectedForGood() =>
        Assert.True(Assert.Throws<DomainRejectedException>(
            () => BudgetAccess.Owned(SharedWithMember(), Member)).Unrecoverable);

    [Fact]
    public void AnArchivedBudgetCanStillBeRestoredSoItsRejectionIsNotFinal() =>
        Assert.False(Assert.Throws<DomainRejectedException>(
            () => BudgetAccess.Writable(Given.Archive(SharedWithMember()))).Unrecoverable);

    [Fact]
    public void AMemberRecordsAnExpenseOwnedByTheBudgetOwner()
    {
        var budget = SharedWithMember();

        var recorded = Assert.IsType<ExpenseRecorded>(Assert.Single(MoneyEntry.Decide(null, budget,
            new RecordExpense(Guid.NewGuid(), Member, Guid.NewGuid(), budget.Id, "Bread", "Food", Pln(5), Given.Today, null),
            Now)));

        Assert.Equal(Given.Owner, recorded.UserId);
    }

    [Fact]
    public void AMemberRecordsAnIncomeOwnedByTheBudgetOwner()
    {
        var budget = SharedWithMember();

        var recorded = Assert.IsType<IncomeRecorded>(Assert.Single(MoneyEntry.Decide(null, budget,
            new RecordIncome(Guid.NewGuid(), Member, Guid.NewGuid(), budget.Id, "Pay", "Salary", Pln(5), Given.Today, null),
            Now)));

        Assert.Equal(Given.Owner, recorded.UserId);
    }

    [Fact]
    public void AMemberEditsAndDeletesTheOwnersEntry()
    {
        var budget = SharedWithMember();
        var entry = Given.AnExpense(budget);

        var edited = Assert.IsType<MoneyEntryEdited>(Assert.Single(MoneyEntry.Decide(entry, budget,
            new EditEntry(Guid.NewGuid(), Member, entry.Id, "Bread", "Food", Pln(7), Given.Today, null), Now)));
        var deleted = Assert.IsType<MoneyEntryDeleted>(Assert.Single(MoneyEntry.Decide(entry, budget,
            new DeleteEntry(Guid.NewGuid(), Member, entry.Id), Now)));

        Assert.Equal(Given.Owner, edited.UserId);
        Assert.Equal(Given.Owner, deleted.UserId);
    }

    [Fact]
    public void AMemberAddsAndRenamesCategories()
    {
        var budget = SharedWithMember();

        var added = Assert.IsType<CategoryAdded>(Assert.Single(Budget.Decide(budget,
            new AddCategory(Guid.NewGuid(), Member, budget.Id, CategoryKind.Expense, "Kids"), Now)));
        var renamed = Assert.IsType<CategoryRenamed>(Assert.Single(Budget.Decide(budget,
            new RenameCategory(Guid.NewGuid(), Member, budget.Id, CategoryKind.Expense, "Food", "Groceries"), Now)));

        Assert.Equal(Given.Owner, added.UserId);
        Assert.Equal(Given.Owner, renamed.UserId);
    }

    [Fact]
    public void AMemberStartsAMonthAndAddsAHolding()
    {
        var budget = SharedWithMember();

        var started = Assert.IsType<BalanceMonthStarted>(Assert.Single(BalanceSnapshot.Decide(null, budget,
            new StartBalanceMonth(Guid.NewGuid(), Member, budget.Id, Given.October), Now)));
        var snapshot = new BalanceSnapshot();
        snapshot.ApplyAll([started]);
        var holding = Given.AHolding();
        var added = Assert.IsType<HoldingAdded>(Assert.Single(BalanceSnapshot.Decide(snapshot, budget,
            new AddHolding(Guid.NewGuid(), Member, budget.Id, Given.October, holding.HoldingId, holding.Name,
                holding.Type, holding.Money), Now)));

        Assert.Equal(Given.Owner, started.UserId);
        Assert.Equal(Given.Owner, added.UserId);
    }

    [Fact]
    public void AStrangerStillReadsAMissingBudget()
    {
        var budget = SharedWithMember();

        Assert.Equal("That budget does not exist.", Assert.Throws<DomainRejectedException>(() => MoneyEntry.Decide(null,
            budget, new RecordExpense(Guid.NewGuid(), Given.Stranger, Guid.NewGuid(), budget.Id, "Bread", "Food", Pln(5),
                Given.Today, null), Now)).Message);
    }

    [Fact]
    public void ARemovedMemberLosesAccess()
    {
        var budget = SharedWithMember();
        budget.ApplyAll(Budget.Decide(budget, new RemoveBudgetMember(Guid.NewGuid(), Given.Owner, budget.Id, Member), Now));

        Assert.Throws<DomainRejectedException>(() => Budget.Decide(budget,
            new AddCategory(Guid.NewGuid(), Member, budget.Id, CategoryKind.Expense, "Kids"), Now));
    }
}
