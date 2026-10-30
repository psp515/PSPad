using PSPad.App.State;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class EntryQueryTests
{
    static readonly Guid BudgetId = Guid.NewGuid();

    [Fact]
    public void ANewExpenseRoundTrips()
    {
        var uri = EntryQuery.ForNew("http://localhost/budgets/x?task=1", BudgetId, CategoryKind.Expense);

        Assert.Equal($"http://localhost/budgets/x?expense={BudgetId}", uri);
        Assert.Equal((BudgetId, CategoryKind.Expense, (YearMonth?)null), EntryQuery.NewFrom(uri));
        Assert.Null(EntryQuery.From(uri));
    }

    [Fact]
    public void AnEntryRoundTrips()
    {
        var entryId = Guid.NewGuid();
        var uri = EntryQuery.For("http://localhost/budgets/x?budget=new", entryId);

        Assert.Equal($"http://localhost/budgets/x?entry={entryId}", uri);
        Assert.Equal(entryId, EntryQuery.From(uri));
        Assert.Null(EntryQuery.NewFrom(uri));
    }

    [Fact]
    public void AnUnrelatedUriOpensNothing()
    {
        Assert.Null(EntryQuery.From("http://localhost/budgets?budget=new"));
        Assert.Null(EntryQuery.NewFrom("http://localhost/budgets?expense=nope"));
    }

    [Fact]
    public void ABadMonthIsIgnored() =>
        Assert.Equal((BudgetId, CategoryKind.Income, (YearMonth?)null),
            EntryQuery.NewFrom($"http://localhost/budgets/x?income={BudgetId}&month=nope"));
}
