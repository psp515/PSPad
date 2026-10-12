using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class CategoryCascadeTests
{
    [Fact]
    public void RenamingRelabelsEveryLiveEntryOfThatKindInThatBudgetOnly()
    {
        var budget = ABudget();
        var other = ABudget();
        var lunch = AnExpense(budget, "Food");
        var dinner = AnExpense(budget, "Food");
        var deleted = Delete(AnExpense(budget, "Food"), budget);
        var income = AnIncome(budget, "Food");
        var elsewhere = AnExpense(other, "Food");
        var car = AnExpense(budget, "Car");

        var staged = CategoryCascade.Rename(budget,
            new RenameCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Food", "Groceries"),
            [lunch, dinner, deleted, income, elsewhere, car], Now);

        Assert.Equal(3, staged.Count);
        Assert.Same(budget, staged[0].Aggregate);
        Assert.IsType<CategoryRenamed>(Assert.Single(staged[0].Events));
        Assert.Equal([lunch.Id, dinner.Id], staged.Skip(1).Select(entry => entry.Aggregate.Id));
        Assert.All(staged.Skip(1), entry =>
            Assert.Equal("Groceries", Assert.IsType<MoneyEntryRecategorised>(Assert.Single(entry.Events)).Category));
        Assert.Equal("Groceries", lunch.Category);
        Assert.Equal("Food", deleted.Category);
        Assert.Equal("Food", income.Category);
        Assert.Equal("Food", elsewhere.Category);
    }

    [Fact]
    public void ARenameThatChangesNothingRelabelsNothing()
    {
        var budget = ABudget();
        var lunch = AnExpense(budget, "Food");

        var staged = CategoryCascade.Rename(budget,
            new RenameCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Food", "Food"), [lunch], Now);

        Assert.Empty(Assert.Single(staged).Events);
    }

    [Fact]
    public void MergingRelabelsIntoTheTargetAndDropsTheSource()
    {
        var budget = ABudget();
        var ride = AnExpense(budget, "Bike");
        var fuel = AnExpense(budget, "Car");

        var staged = CategoryCascade.Merge(budget,
            new MergeCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Bike", "Car"), [ride, fuel], Now);

        Assert.Equal(2, staged.Count);
        Assert.IsType<CategoriesMerged>(Assert.Single(staged[0].Events));
        Assert.Equal(ride.Id, staged[1].Aggregate.Id);
        Assert.Equal("Car", ride.Category);
        Assert.DoesNotContain("Bike", budget.ExpenseCategories);
    }

    [Fact]
    public void RemovingAUsedCategoryIsRejected()
    {
        var budget = ABudget();
        var gift = AnExpense(budget, "Gifts");

        var rejection = Assert.Throws<DomainRejectedException>(() => CategoryCascade.Remove(budget,
            new RemoveCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "gifts"), [gift], Now));

        Assert.Equal("That category still has entries.", rejection.Message);
        Assert.Contains("Gifts", budget.ExpenseCategories);
    }

    [Fact]
    public void RemovingIsAllowedOnceItsEntriesAreDeleted()
    {
        var budget = ABudget();
        var gift = Delete(AnExpense(budget, "Gifts"), budget);

        var staged = CategoryCascade.Remove(budget,
            new RemoveCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Gifts"), [gift], Now);

        Assert.IsType<CategoryRemoved>(Assert.Single(Assert.Single(staged).Events));
        Assert.DoesNotContain("Gifts", budget.ExpenseCategories);
    }

    [Fact]
    public void RemovingIgnoresEntriesOfTheOtherKind()
    {
        var budget = ABudget();
        var present = AnIncome(budget, "Gifts");

        CategoryCascade.Remove(budget,
            new RemoveCategory(Guid.NewGuid(), Owner, budget.Id, CategoryKind.Expense, "Gifts"), [present], Now);

        Assert.DoesNotContain("Gifts", budget.ExpenseCategories);
        Assert.Contains("Gifts", budget.IncomeCategories);
    }

    [Fact]
    public void RemovingFromAMissingBudgetIsRejectedBeforeLookingAtEntries() =>
        Assert.Equal("That budget does not exist.", Assert.Throws<DomainRejectedException>(() => CategoryCascade.Remove(null,
            new RemoveCategory(Guid.NewGuid(), Owner, Guid.NewGuid(), CategoryKind.Expense, "Gifts"), [], Now)).Message);
}
