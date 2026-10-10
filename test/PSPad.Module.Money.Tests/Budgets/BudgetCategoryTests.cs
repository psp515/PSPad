using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class BudgetCategoryTests
{
    static RenameCategory Rename(Budget budget, string from, string to, Guid? actor = null) =>
        new(Guid.NewGuid(), actor ?? Owner, budget.Id, CategoryKind.Expense, from, to);

    static MergeCategory Merge(Budget budget, string from, string into) =>
        new(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, from, into);

    static RemoveCategory Remove(Budget budget, string name, CategoryKind kind = CategoryKind.Expense) =>
        new(Guid.NewGuid(), Owner, budget.Id, kind, name);

    static string Rejection(Budget? budget, ICommand command) =>
        Assert.Throws<DomainRejectedException>(() => Budget.Decide(budget, command, Now)).Message;

    [Fact]
    public void RenamingReplacesTheNameInPlace()
    {
        var budget = ABudget();

        budget.ApplyAll(Budget.Decide(budget, Rename(budget, "food", " Groceries "), Now));

        Assert.Equal(["Home", "Groceries", "Eating Out", "PC", "Bike", "Car", "Gifts", "Clothes"], budget.ExpenseCategories);
    }

    [Fact]
    public void TheRenameEventCarriesTheStoredSpelling()
    {
        var budget = ABudget();

        var renamed = Assert.IsType<CategoryRenamed>(Assert.Single(Budget.Decide(budget, Rename(budget, "food", "Groceries"), Now)));

        Assert.Equal((CategoryKind.Expense, "Food", "Groceries"), (renamed.Kind, renamed.From, renamed.To));
    }

    [Fact]
    public void ACaseOnlyRenameIsAllowed()
    {
        var budget = ABudget();

        budget.ApplyAll(Budget.Decide(budget, Rename(budget, "PC", "Pc"), Now));

        Assert.Contains("Pc", budget.ExpenseCategories);
        Assert.DoesNotContain("PC", budget.ExpenseCategories);
    }

    [Fact]
    public void RenamingToTheSameSpellingEmitsNothing()
    {
        var budget = ABudget();
        Assert.Empty(Budget.Decide(budget, Rename(budget, "Food", " Food "), Now));
    }

    [Fact]
    public void RenamingOntoAnotherCategoryIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("That category already exists. Merge them instead.", Rejection(budget, Rename(budget, "Food", "home")));
    }

    [Fact]
    public void RenamingAMissingCategoryIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("That category does not exist.", Rejection(budget, Rename(budget, "Hobby", "Fun")));
    }

    [Fact]
    public void RenamingToABlankNameIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("A category needs a name.", Rejection(budget, Rename(budget, "Food", "  ")));
    }

    [Fact]
    public void RenamingInAnArchivedBudgetIsRejected()
    {
        var budget = Archive(ABudget());
        Assert.Equal("This budget is archived.", Rejection(budget, Rename(budget, "Food", "Groceries")));
    }

    [Fact]
    public void AStrangerCannotRename()
    {
        var budget = ABudget();
        Assert.Equal("That budget does not exist.", Rejection(budget, Rename(budget, "Food", "Groceries", Stranger)));
    }

    [Fact]
    public void MergingRemovesTheSource()
    {
        var budget = ABudget();

        var merged = Assert.IsType<CategoriesMerged>(Assert.Single(Budget.Decide(budget, Merge(budget, "bike", "CAR"), Now)));
        budget.ApplyAll([merged]);

        Assert.Equal(("Bike", "Car"), (merged.From, merged.Into));
        Assert.Equal(["Home", "Food", "Eating Out", "PC", "Car", "Gifts", "Clothes"], budget.ExpenseCategories);
    }

    [Fact]
    public void MergingIntoItselfIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("A category cannot be merged into itself.", Rejection(budget, Merge(budget, "Car", "car")));
    }

    [Theory]
    [InlineData("Hobby", "Car")]
    [InlineData("Car", "Hobby")]
    public void MergingWithAMissingCategoryIsRejected(string from, string into)
    {
        var budget = ABudget();
        Assert.Equal("That category does not exist.", Rejection(budget, Merge(budget, from, into)));
    }

    [Fact]
    public void MergingInAnArchivedBudgetIsRejected()
    {
        var budget = Archive(ABudget());
        Assert.Equal("This budget is archived.", Rejection(budget, Merge(budget, "Bike", "Car")));
    }

    [Fact]
    public void RemovingDropsTheName()
    {
        var budget = ABudget();

        budget.ApplyAll(Budget.Decide(budget, Remove(budget, "other", CategoryKind.Income), Now));

        Assert.Equal(["Salary", "Freelance", "Interest", "Gifts"], budget.IncomeCategories);
    }

    [Fact]
    public void RemovingAMissingCategoryIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("That category does not exist.", Rejection(budget, Remove(budget, "Hobby")));
    }

    [Fact]
    public void RemovingInAnArchivedBudgetIsRejected()
    {
        var budget = Archive(ABudget());
        Assert.Equal("This budget is archived.", Rejection(budget, Remove(budget, "PC")));
    }

    [Fact]
    public void AnUnknownCommandIsRejected()
    {
        var budget = ABudget();
        Assert.Equal("A budget cannot handle DeleteEntry.",
            Rejection(budget, new DeleteEntry(Guid.NewGuid(), Owner, Guid.NewGuid())));
    }
}
