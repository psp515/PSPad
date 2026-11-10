using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Entries;

[UnitTest]
public class PurchaseWithReturnsTests
{
    static readonly DateOnly Day5 = new(2026, 10, 5);
    static readonly DateOnly Day8 = new(2026, 10, 8);

    static EditEntry Edit(MoneyEntry entry, Budget budget, Money? money = null, DateOnly? date = null,
        string? name = null, string? category = null) =>
        new(Guid.NewGuid(), budget.UserId, entry.Id, name ?? entry.Name, category ?? entry.Category,
            money ?? entry.Money, date ?? entry.Date, null);

    static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Write(
        MoneyEntry entry, Budget budget, IReadOnlyList<MoneyEntry> entries, ICommand command) =>
        MoneyEntryCascade.Write(entry, budget, entries, command, Now);

    static string Rejection(Action act) => Assert.Throws<DomainRejectedException>(act).Message;

    static (Budget Budget, MoneyEntry Purchase, MoneyEntry Return) Purchased(decimal returned = 150m)
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Clothes", 420m, new DateOnly(2026, 10, 1), name: "Shoes");
        return (budget, purchase, AReturn(purchase, budget, returned, Day5));
    }

    [Fact]
    public void EditingAReturnAsAnEntryIsRejected()
    {
        var (budget, purchase, returned) = Purchased();

        var message = Rejection(() =>
            Write(returned, budget, [purchase, returned], Edit(returned, budget, name: "Other")));

        Assert.Equal("Edit the return instead.", message);
    }

    [Fact]
    public void ThePurchaseCantDropBelowWhatWasReturned()
    {
        var (budget, purchase, returned) = Purchased(150m);
        var below = purchase.Money with { Amount = 149m };
        var exact = purchase.Money with { Amount = 150m };

        var message = Rejection(() => Write(purchase, budget, [purchase, returned], Edit(purchase, budget, below)));
        var staged = Write(purchase, budget, [purchase, returned], Edit(purchase, budget, exact));

        Assert.Equal("The amount can't be less than what was returned.", message);
        Assert.Equal(150m, purchase.Money.Amount);
        Assert.IsType<MoneyEntryEdited>(Assert.Single(staged).Events.Single());
    }

    [Fact]
    public void ThePurchaseKeepsItsCurrencyAndRate()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Clothes", money: new Money(100m, "EUR", 4.2m, Today));
        var returned = AReturn(purchase, budget, 10m);

        var rate = Rejection(() => Write(purchase, budget, [purchase, returned],
            Edit(purchase, budget, purchase.Money with { RateToPln = 4.30m })));
        var currency = Rejection(() => Write(purchase, budget, [purchase, returned],
            Edit(purchase, budget, new Money(100m, "PLN", 1m, Today))));

        Assert.Equal("A purchase with returns keeps its currency and rate.", rate);
        Assert.Equal("A purchase with returns keeps its currency and rate.", currency);
    }

    [Fact]
    public void ThePurchaseCantMoveAfterItsReturns()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Clothes", 100m, new DateOnly(2026, 10, 1));
        var first = AReturn(purchase, budget, 10m, Day5);
        var second = AReturn(purchase, budget, 10m, Day8, [first]);
        IReadOnlyList<MoneyEntry> all = [purchase, first, second];

        var message = Rejection(() => Write(purchase, budget, all, Edit(purchase, budget, date: new DateOnly(2026, 10, 6))));
        var staged = Write(purchase, budget, all, Edit(purchase, budget, date: Day5));

        Assert.Equal("A purchase can't be dated after its returns.", message);
        Assert.IsType<MoneyEntryEdited>(staged[0].Events.Single());
    }

    [Fact]
    public void APurchaseWithoutReturnsEditsFreely()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Clothes", 100m);

        var staged = Write(purchase, budget, [purchase],
            Edit(purchase, budget, new Money(5m, "EUR", 4.3m, Today)));

        Assert.IsType<MoneyEntryEdited>(Assert.Single(staged).Events.Single());
        Assert.Equal("EUR", purchase.Money.Currency);
    }

    [Fact]
    public void RenamingOrRecategorisingThePurchaseRelabelsItsReturns()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Clothes", 420m, name: "Shoes");
        var first = AReturn(purchase, budget, 10m);
        var second = AReturn(purchase, budget, 20m, others: [first]);
        var gone = Delete(AReturn(purchase, budget, 5m, others: [first, second]), budget);

        var staged = Write(purchase, budget, [purchase, first, second, gone],
            Edit(purchase, budget, name: "Running shoes", category: "Food"));

        Assert.Contains(staged, written => written.Aggregate == purchase && written.Events.Single() is MoneyEntryEdited);
        var relabels = staged.Where(written => written.Events.Any(change => change is MoneyEntryRelabelled)).ToArray();
        Assert.Equal(new[] { first.Id, second.Id }.Order(), relabels.Select(written => written.Aggregate.Id).Order());
        Assert.All(relabels, written =>
        {
            var relabelled = Assert.IsType<MoneyEntryRelabelled>(Assert.Single(written.Events));
            Assert.Equal("Running shoes", relabelled.Name);
            Assert.Equal("Food", relabelled.Category);
        });
        Assert.All(new[] { first, second }, returned =>
        {
            Assert.Equal("Running shoes", returned.Name);
            Assert.Equal("Food", returned.Category);
        });
        Assert.Equal("Shoes", gone.Name);
    }

    [Fact]
    public void AnEditThatKeepsNameAndCategoryRelabelsNothing()
    {
        var (budget, purchase, returned) = Purchased(100m);

        var staged = Write(purchase, budget, [purchase, returned],
            Edit(purchase, budget, purchase.Money with { Amount = 300m }));

        Assert.DoesNotContain(staged.SelectMany(written => written.Events), change => change is MoneyEntryRelabelled);
    }

    [Fact]
    public void DeletingThePurchaseDeletesItsLiveReturns()
    {
        var budget = ABudget();
        var purchase = AnExpense(budget, "Clothes", 420m);
        var first = AReturn(purchase, budget, 10m);
        var second = AReturn(purchase, budget, 20m, others: [first]);
        var gone = Delete(AReturn(purchase, budget, 5m, others: [first, second]), budget);

        var staged = Write(purchase, budget, [purchase, first, second, gone],
            new DeleteEntry(Guid.NewGuid(), Owner, purchase.Id));

        Assert.Equal(new[] { purchase.Id, first.Id, second.Id }.Order(), staged.Select(written => written.Aggregate.Id).Order());
        Assert.All(staged, written => Assert.IsType<MoneyEntryDeleted>(Assert.Single(written.Events)));
        Assert.True(first.Deleted);
        Assert.True(second.Deleted);
    }

    [Fact]
    public void DeletingAReturnLeavesThePurchase()
    {
        var (budget, purchase, returned) = Purchased();

        var staged = Write(returned, budget, [purchase, returned], new DeleteEntry(Guid.NewGuid(), Owner, returned.Id));

        Assert.Equal(returned.Id, Assert.Single(staged).Aggregate.Id);
        Assert.True(returned.Deleted);
        Assert.False(purchase.Deleted);
    }

    [Fact]
    public void RenamingACategoryRelabelsReturns()
    {
        var (budget, purchase, returned) = Purchased();

        var staged = CategoryCascade.Rename(budget,
            new RenameCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Clothes", "Apparel"),
            [purchase, returned], Now);

        Assert.Contains(staged, written => written.Aggregate == returned && written.Events.Single() is MoneyEntryRecategorised);
        Assert.Equal("Apparel", returned.Category);
    }

    [Fact]
    public void AReturnKeepsItsPurchasesCategoryInUseSoItCantBeRemoved()
    {
        var (budget, purchase, returned) = Purchased();

        var message = Rejection(() => CategoryCascade.Remove(budget,
            new RemoveCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Clothes"),
            [purchase, returned], Now));

        Assert.Equal("That category still has entries.", message);
        Assert.True(returned.Uses(CategoryKind.Expense, "Clothes"));
    }
}
