using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Entries;

[UnitTest]
public class MoneyEntryTests
{
    static RecordExpense Expense(Budget budget, string name = "Groceries", string category = "Food", Money? money = null,
        string? note = null, Guid? entryId = null) =>
        new(Guid.NewGuid(), Owner, entryId ?? Guid.NewGuid(), budget.Id, name, category,
            money ?? new Money(10m, "PLN", 1m, Today), Today, note);

    static EditEntry Edit(MoneyEntry entry, string name = "Groceries", string category = "Food", Money? money = null,
        DateOnly? date = null, string? note = null) =>
        new(Guid.NewGuid(), Owner, entry.Id, name, category, money ?? entry.Money, date ?? entry.Date, note);

    static string Rejection(MoneyEntry? entry, Budget? budget, ICommand command) =>
        Assert.Throws<DomainRejectedException>(() => MoneyEntry.Decide(entry, budget, command, Now)).Message;

    [Fact]
    public void RecordingAnExpenseFoldsEveryField()
    {
        var budget = ABudget();
        var entry = new MoneyEntry();
        var command = Expense(budget, name: "  Lidl ", money: new Money(12.5m, "eur", 4.25m, new DateOnly(2026, 10, 8)),
            note: "  weekly shop ");

        entry.ApplyAll(MoneyEntry.Decide(null, budget, command, Now));

        Assert.Equal(command.EntryId, entry.Id);
        Assert.Equal(budget.UserId, entry.UserId);
        Assert.Equal(budget.Id, entry.BudgetId);
        Assert.Equal(CategoryKind.Expense, entry.Kind);
        Assert.Equal("Lidl", entry.Name);
        Assert.Equal("Food", entry.Category);
        Assert.Equal(new Money(12.5m, "EUR", 4.25m, new DateOnly(2026, 10, 8)), entry.Money);
        Assert.Equal(Today, entry.Date);
        Assert.Equal("weekly shop", entry.Note);
        Assert.Equal(Now, entry.RecordedAt);
        Assert.False(entry.Deleted);
    }

    [Fact]
    public void RecordingAnIncomeUsesTheIncomeKind()
    {
        var entry = AnIncome(ABudget(), "salary");

        Assert.Equal(CategoryKind.Income, entry.Kind);
        Assert.Equal("Salary", entry.Category);
    }

    [Fact]
    public void TheCategoryTakesTheBudgetsSpelling() =>
        Assert.Equal("Eating Out", AnExpense(ABudget(), "eating out").Category);

    [Fact]
    public void ANewCategoryKeepsItsTypedSpelling() =>
        Assert.Equal("Hobby", AnExpense(ABudget(), " Hobby ").Category);

    [Fact]
    public void APlnAmountIsForcedToRateOneOnTheEntryDate()
    {
        var entry = AnExpense(ABudget(), money: new Money(10m, "pln", 3m, new DateOnly(2020, 1, 1)));

        Assert.Equal(new Money(10m, "PLN", 1m, Today), entry.Money);
    }

    [Fact]
    public void ABlankNoteIsStoredAsNone()
    {
        var budget = ABudget();
        var entry = new MoneyEntry();

        entry.ApplyAll(MoneyEntry.Decide(null, budget, Expense(budget, note: "   "), Now));

        Assert.Null(entry.Note);
    }

    [Theory]
    [InlineData("", "An entry needs a name.")]
    [InlineData("   ", "An entry needs a name.")]
    public void ABlankNameIsRejected(string name, string message)
    {
        var budget = ABudget();
        Assert.Equal(message, Rejection(null, budget, Expense(budget, name: name)));
    }

    [Fact]
    public void ANameOver120CharactersIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("An entry name is at most 120 characters.",
            Rejection(null, budget, Expense(budget, name: new string('n', 121))));
    }

    [Fact]
    public void ANameOfExactly120CharactersIsAllowed() =>
        Assert.Equal(120, AnExpense(ABudget(), name: new string('n', 120)).Name.Length);

    [Fact]
    public void AZeroAmountIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("An amount has to be more than zero.",
            Rejection(null, budget, Expense(budget, money: new Money(0m, "PLN", 1m, Today))));
    }

    [Fact]
    public void AForeignAmountWithoutARateIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("A rate to PLN has to be more than zero.",
            Rejection(null, budget, Expense(budget, money: new Money(5m, "EUR", 0m, Today))));
    }

    [Fact]
    public void ABlankCategoryIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("A category needs a name.", Rejection(null, budget, Expense(budget, category: " ")));
    }

    [Fact]
    public void RecordingIntoAMissingBudgetIsRejected() =>
        Assert.Equal("That budget does not exist.", Rejection(null, null, Expense(ABudget())));

    [Fact]
    public void RecordingIntoSomebodyElsesBudgetIsRejected()
    {
        var budget = ABudget();
        var command = Expense(budget) with { UserId = Stranger };

        Assert.Equal("That budget does not exist.", Rejection(null, budget, command));
    }

    [Fact]
    public void RecordingIntoAnArchivedBudgetIsRejected()
    {
        var budget = Archive(ABudget());
        Assert.Equal("This budget is archived.", Rejection(null, budget, Expense(budget)));
    }

    [Fact]
    public void RecordingAnExistingIdIsRejected()
    {
        var budget = ABudget();
        var entry = AnExpense(budget);

        Assert.Equal("That entry already exists.", Rejection(entry, budget, Expense(budget, entryId: entry.Id)));
    }

    [Fact]
    public void EditingReplacesTheFields()
    {
        var budget = ABudget();
        var entry = AnExpense(budget);

        entry.ApplyAll(MoneyEntry.Decide(entry, budget,
            Edit(entry, " Biedronka ", "home", new Money(20m, "EUR", 4.3m, new DateOnly(2026, 10, 7)),
                new DateOnly(2026, 10, 7), " note "), Now));

        Assert.Equal("Biedronka", entry.Name);
        Assert.Equal("Home", entry.Category);
        Assert.Equal(new Money(20m, "EUR", 4.3m, new DateOnly(2026, 10, 7)), entry.Money);
        Assert.Equal(new DateOnly(2026, 10, 7), entry.Date);
        Assert.Equal("note", entry.Note);
        Assert.Equal(CategoryKind.Expense, entry.Kind);
    }

    [Fact]
    public void EditingWithNoChangeEmitsNothing()
    {
        var budget = ABudget();
        var entry = AnExpense(budget);

        Assert.Empty(MoneyEntry.Decide(entry, budget, Edit(entry), Now));
    }

    [Fact]
    public void EditingAMissingEntryIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("That entry does not exist.",
            Rejection(null, budget, new EditEntry(Guid.NewGuid(), Owner, Guid.NewGuid(), "x", "Food",
                new Money(1m, "PLN", 1m, Today), Today, null)));
    }

    [Fact]
    public void EditingADeletedEntryIsRejected()
    {
        var budget = ABudget();
        var entry = Delete(AnExpense(budget), budget);

        Assert.Equal("That entry does not exist.", Rejection(entry, budget, Edit(entry)));
    }

    [Fact]
    public void EditingInAnArchivedBudgetIsRejected()
    {
        var budget = ABudget();
        var entry = AnExpense(budget);
        Archive(budget);

        Assert.Equal("This budget is archived.", Rejection(entry, budget, Edit(entry, name: "Other")));
    }

    [Fact]
    public void DeletingSoftDeletes()
    {
        var budget = ABudget();
        var entry = Delete(AnExpense(budget), budget);

        Assert.True(entry.Deleted);
        Assert.False(entry.Uses(CategoryKind.Expense, "Food"));
    }

    [Fact]
    public void DeletingTwiceEmitsNothing()
    {
        var budget = ABudget();
        var entry = Delete(AnExpense(budget), budget);

        Assert.Empty(MoneyEntry.Decide(entry, budget, new DeleteEntry(Guid.NewGuid(), Owner, entry.Id), Now));
    }

    [Fact]
    public void DeletingAMissingEntryIsRejected() =>
        Assert.Equal("That entry does not exist.",
            Rejection(null, ABudget(), new DeleteEntry(Guid.NewGuid(), Owner, Guid.NewGuid())));

    [Fact]
    public void DeletingInAnArchivedBudgetIsRejected()
    {
        var budget = ABudget();
        var entry = AnExpense(budget);
        Archive(budget);

        Assert.Equal("This budget is archived.",
            Rejection(entry, budget, new DeleteEntry(Guid.NewGuid(), Owner, entry.Id)));
    }

    [Fact]
    public void UsesMatchesKindAndNameIgnoringCase()
    {
        var entry = AnExpense(ABudget(), "Gifts");

        Assert.True(entry.Uses(CategoryKind.Expense, " gifts "));
        Assert.False(entry.Uses(CategoryKind.Income, "Gifts"));
        Assert.False(entry.Uses(CategoryKind.Expense, "Food"));
    }

    [Fact]
    public void AnUnknownCommandIsRejected() =>
        Assert.Equal("A money entry cannot handle CreateBudget.",
            Rejection(null, ABudget(), new CreateBudget(Guid.NewGuid(), Owner, Guid.NewGuid(), "x")));
}
