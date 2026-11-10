using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Entries;

[UnitTest]
public class ReturnTests
{
    static readonly DateOnly Bought = new(2026, 10, 2);

    static string Rejection(Func<IReadOnlyList<DomainEvent>> decide) =>
        Assert.Throws<DomainRejectedException>(() => decide()).Message;

    static RecordReturn Return(MoneyEntry purchase, decimal amount, DateOnly? date = null, string? note = null,
        Guid? entryId = null, Guid? by = null) =>
        new(Guid.NewGuid(), by ?? Owner, entryId ?? Guid.NewGuid(), purchase.Id, amount, date ?? purchase.Date, note);

    static EditReturn Edit(MoneyEntry returned, decimal amount, DateOnly? date = null, string? note = null) =>
        new(Guid.NewGuid(), Owner, returned.Id, amount, date ?? returned.Date, note);

    static IReadOnlyList<DomainEvent> Record(MoneyEntry? purchase, Budget? budget, RecordReturn command,
        MoneyEntry? entry = null, params MoneyEntry[] others) =>
        MoneyEntry.DecideReturn(entry, purchase, budget, purchase is null ? others : [purchase, .. others], command, Now);

    static IReadOnlyList<DomainEvent> Change(MoneyEntry returned, MoneyEntry? purchase, Budget budget, EditReturn command,
        params MoneyEntry[] others) =>
        MoneyEntry.DecideReturn(returned, purchase, budget, [.. purchase is null ? [] : new[] { purchase }, returned, .. others], command, Now);

    [Fact]
    public void AReturnCopiesThePurchaseAndKeepsItsOwnAmountDateAndNote()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Food", date: Bought,
            money: new Money(120m, "EUR", 4.268m, new DateOnly(2026, 10, 1)), name: "Headphones");
        var returned = new MoneyEntry();
        var command = Return(purchase, 50m, Today, "  ear cushions ");

        returned.ApplyAll(MoneyEntry.DecideReturn(null, purchase, budget, [purchase], command, Now));

        Assert.Equal(command.EntryId, returned.Id);
        Assert.Equal(purchase.Id, returned.ReturnOf);
        Assert.True(returned.IsReturn);
        Assert.Equal(CategoryKind.Expense, returned.Kind);
        Assert.Equal(budget.Id, returned.BudgetId);
        Assert.Equal(budget.UserId, returned.UserId);
        Assert.Equal("Headphones", returned.Name);
        Assert.Equal("Food", returned.Category);
        Assert.Equal(new Money(50m, "EUR", 4.268m, new DateOnly(2026, 10, 1)), returned.Money);
        Assert.Equal(Today, returned.Date);
        Assert.Equal("ear cushions", returned.Note);
        Assert.Equal(-213.40m, returned.SignedPln);
    }

    [Fact]
    public void APlnReturnIsDatedByItsOwnDay()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, date: Bought, amount: 80m);

        var returned = AReturn(purchase, budget, 30m, Today);

        Assert.Equal(new Money(30m, "PLN", 1m, Today), returned.Money);
    }

    [Fact]
    public void AFullReturnCancelsThePurchaseInPln()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, date: Bought, money: new Money(120m, "EUR", 4.268m, Bought));

        var returned = AReturn(purchase, budget, 120m, Today);

        Assert.Equal(0m, purchase.SignedPln + returned.SignedPln);
    }

    [Fact]
    public void ReturnStateSumsLiveReturnsOnly()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 420m, date: Bought);
        var kept = AReturn(purchase, budget, 150m);
        var removed = Delete(AReturn(purchase, budget, 100m), budget);

        var state = ReturnState.Of(purchase, [purchase, kept, removed]);

        Assert.Equal(150m, state.Returned);
        Assert.Equal(270m, state.Left);
        Assert.False(state.IsFull);
        Assert.Equal([kept], state.Returns);
    }

    [Fact]
    public void ReturnStateListsNewestFirst()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var earlier = AReturn(purchase, budget, 10m, new DateOnly(2026, 10, 3));
        var later = AReturn(purchase, budget, 10m, new DateOnly(2026, 10, 5));

        var state = ReturnState.Of(purchase, [earlier, later]);

        Assert.Equal(new DateOnly(2026, 10, 5), state.Returns[0].Date);
    }

    [Fact]
    public void ReturnStateIsFullWhenNothingIsLeft()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var returned = AReturn(purchase, budget, 100m);

        var state = ReturnState.Of(purchase, [purchase, returned]);

        Assert.True(state.IsFull);
        Assert.Equal(0m, state.Left);
    }

    [Fact]
    public void ReturnStateIgnoresOtherPurchasesReturns()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var other = AnExpense(budget, amount: 100m, date: Bought);
        var foreign = AReturn(other, budget, 70m);

        var state = ReturnState.Of(purchase, [purchase, other, foreign]);

        Assert.Empty(state.Returns);
        Assert.Equal(0m, state.Returned);
        Assert.Equal(100m, state.Left);
    }

    [Fact]
    public void AMissingPurchaseIsUnrecoverable()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);

        var rejection = Assert.Throws<DomainRejectedException>(() => Record(null, budget, Return(purchase, 5m)));

        Assert.Equal("That entry does not exist.", rejection.Message);
        Assert.True(rejection.Unrecoverable);
    }

    [Fact]
    public void ADeletedPurchaseCannotBeReturned()
    {
        var budget = ABudget();
        var purchase = Delete(AnExpense(budget), budget);

        Assert.Equal("That entry does not exist.", Rejection(() => Record(purchase, budget, Return(purchase, 5m))));
    }

    [Fact]
    public void AStrangerCannotReturnAPurchase()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);

        Assert.Equal("That budget does not exist.",
            Rejection(() => Record(purchase, budget, Return(purchase, 5m, by: Stranger))));
    }

    [Fact]
    public void AnArchivedBudgetRejectsAReturn()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);
        Archive(budget);

        Assert.Equal("This budget is archived.", Rejection(() => Record(purchase, budget, Return(purchase, 5m))));
    }

    [Fact]
    public void ATakenEntryIdIsRejected()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);
        var taken = AnExpense(budget);

        Assert.Equal("That entry already exists.",
            Rejection(() => Record(purchase, budget, Return(purchase, 5m, entryId: taken.Id), taken)));
    }

    [Fact]
    public void AnIncomeCannotBeReturned()
    {
        var budget = ABudget();
        var income = AnIncome(budget);

        Assert.Equal("Only an expense can be returned.", Rejection(() => Record(income, budget, Return(income, 5m))));
    }

    [Fact]
    public void AReturnCannotBeReturned()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m);
        var returned = AReturn(purchase, budget, 50m);

        Assert.Equal("Only an expense can be returned.",
            Rejection(() => Record(returned, budget, Return(returned, 5m))));
    }

    [Fact]
    public void AZeroAmountIsRejected()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);

        Assert.Equal("An amount has to be more than zero.", Rejection(() => Record(purchase, budget, Return(purchase, 0m))));
    }

    [Fact]
    public void AReturnCannotPrecedeItsPurchase()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, date: Bought);

        Assert.Equal("A return can't be dated before its purchase.",
            Rejection(() => Record(purchase, budget, Return(purchase, 5m, Bought.AddDays(-1)))));
    }

    [Fact]
    public void AReturnCannotExceedWhatIsLeft()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var earlier = AReturn(purchase, budget, 60m);

        Assert.Equal("That is more than is left to return.",
            Rejection(() => Record(purchase, budget, Return(purchase, 41m), null, earlier)));
    }

    [Fact]
    public void AReturnOfExactlyWhatIsLeftIsAccepted()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var earlier = AReturn(purchase, budget, 60m);

        var events = Record(purchase, budget, Return(purchase, 40m), null, earlier);

        Assert.IsType<ReturnRecorded>(Assert.Single(events));
    }

    [Fact]
    public void ATooLongNoteIsRejected()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);
        var note = new string('n', MoneyEntry.MaxNoteLength + 1);

        Assert.Equal($"A note is at most {MoneyEntry.MaxNoteLength} characters.",
            Rejection(() => Record(purchase, budget, Return(purchase, 5m, note: note))));
    }

    [Fact]
    public void AMemberOfASharedBudgetCanRecordAReturn()
    {
        var budget = Join(Share(ABudget()));
        var purchase = AnExpense(budget);

        var events = Record(purchase, budget, Return(purchase, 5m, by: Member));

        Assert.Equal(budget.UserId, Assert.Single(events).UserId);
    }

    [Fact]
    public void EditingAReturnChangesAmountDateAndNote()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, date: Bought, money: new Money(100m, "EUR", 4.3m, Bought));
        var returned = AReturn(purchase, budget, 20m, Bought);

        var events = Change(returned, purchase, budget, Edit(returned, 30m, Today, " changed "));
        returned.ApplyAll(events);

        Assert.IsType<ReturnEdited>(Assert.Single(events));
        Assert.Equal(30m, returned.Money.Amount);
        Assert.Equal(Today, returned.Date);
        Assert.Equal("changed", returned.Note);
        Assert.Equal("EUR", returned.Money.Currency);
        Assert.Equal(4.3m, returned.Money.RateToPln);
    }

    [Fact]
    public void EditingAReturnCanUseItsOwnShare()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var first = AReturn(purchase, budget, 60m);
        var second = AReturn(purchase, budget, 40m);

        var events = Change(second, purchase, budget, Edit(second, 40m, note: "again"), first);

        Assert.IsType<ReturnEdited>(Assert.Single(events));
        Assert.Equal("That is more than is left to return.",
            Rejection(() => Change(second, purchase, budget, Edit(second, 41m), first)));
    }

    [Fact]
    public void AnUnchangedEditEmitsNothing()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var returned = AReturn(purchase, budget, 40m);

        Assert.Empty(Change(returned, purchase, budget, Edit(returned, 40m)));
    }

    [Fact]
    public void EditingAnEntryThatIsNotAReturnIsRejected()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget);

        Assert.Equal("That entry does not exist.",
            Rejection(() => Change(purchase, null, budget, Edit(purchase, 5m))));
    }

    [Fact]
    public void EditingAReturnOfADeletedPurchaseIsRejected()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m);
        var returned = AReturn(purchase, budget, 40m);
        Delete(purchase, budget);

        Assert.Equal("That entry does not exist.",
            Rejection(() => Change(returned, purchase, budget, Edit(returned, 30m))));
    }

    [Fact]
    public void EditingAReturnCannotMoveItBeforeItsPurchase()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m, date: Bought);
        var returned = AReturn(purchase, budget, 40m);

        Assert.Equal("A return can't be dated before its purchase.",
            Rejection(() => Change(returned, purchase, budget, Edit(returned, 40m, Bought.AddDays(-1)))));
    }

    [Fact]
    public void EditingAReturnInAnArchivedBudgetIsRejected()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, amount: 100m);
        var returned = AReturn(purchase, budget, 40m);
        Archive(budget);

        Assert.Equal("This budget is archived.",
            Rejection(() => Change(returned, purchase, budget, Edit(returned, 30m))));
    }
}
