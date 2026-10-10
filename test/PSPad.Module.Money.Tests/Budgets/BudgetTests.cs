using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class BudgetTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");
    static readonly Guid BudgetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    static Budget Existing(bool archived = false)
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), Owner, BudgetId, "Personal"), Now));
        if (archived)
        {
            budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), Owner, BudgetId), Now));
        }

        return budget;
    }

    static string Rejection(Budget? budget, ICommand command) =>
        Assert.Throws<DomainRejectedException>(() => Budget.Decide(budget, command, Now)).Message;

    [Fact]
    public void CreatingSeedsBothCategoryLists()
    {
        var budget = Existing();

        Assert.Equal("Personal", budget.Name);
        Assert.Equal(Owner, budget.UserId);
        Assert.Equal(Now, budget.CreatedAt);
        Assert.Equal(["Home", "Food", "Eating Out", "PC", "Bike", "Car", "Gifts", "Clothes"], budget.ExpenseCategories);
        Assert.Equal(["Salary", "Freelance", "Interest", "Gifts", "Other"], budget.IncomeCategories);
        Assert.False(budget.IsArchived);
    }

    [Fact]
    public void TheNameIsTrimmed()
    {
        var created = Assert.IsType<BudgetCreated>(Assert.Single(
            Budget.Decide(null, new CreateBudget(Guid.NewGuid(), Owner, BudgetId, "  Household "), Now)));

        Assert.Equal("Household", created.Name);
    }

    [Theory]
    [InlineData("", "A budget needs a name.")]
    [InlineData("   ", "A budget needs a name.")]
    public void ABlankNameIsRejected(string name, string message) =>
        Assert.Equal(message, Rejection(null, new CreateBudget(Guid.NewGuid(), Owner, BudgetId, name)));

    [Fact]
    public void ANameOver80CharactersIsRejected() =>
        Assert.Equal("A budget name is at most 80 characters.",
            Rejection(null, new CreateBudget(Guid.NewGuid(), Owner, BudgetId, new string('b', 81))));

    [Fact]
    public void CreatingAnExistingIdIsRejected() =>
        Assert.Equal("That budget already exists.",
            Rejection(Existing(), new CreateBudget(Guid.NewGuid(), Owner, BudgetId, "Again")));

    [Fact]
    public void RenamingChangesTheName()
    {
        var budget = Existing();

        budget.ApplyAll(Budget.Decide(budget, new RenameBudget(Guid.NewGuid(), Owner, BudgetId, "Household"), Now));

        Assert.Equal("Household", budget.Name);
    }

    [Fact]
    public void RenamingToTheSameNameEmitsNothing() =>
        Assert.Empty(Budget.Decide(Existing(), new RenameBudget(Guid.NewGuid(), Owner, BudgetId, "Personal"), Now));

    [Fact]
    public void ArchivingAndRestoringToggleArchivedAt()
    {
        var budget = Existing(archived: true);
        Assert.Equal(Now, budget.ArchivedAt);

        budget.ApplyAll(Budget.Decide(budget, new RestoreBudget(Guid.NewGuid(), Owner, BudgetId), Now));
        Assert.Null(budget.ArchivedAt);
    }

    [Fact]
    public void ArchivingTwiceAndRestoringALiveBudgetEmitNothing()
    {
        Assert.Empty(Budget.Decide(Existing(archived: true), new ArchiveBudget(Guid.NewGuid(), Owner, BudgetId), Now));
        Assert.Empty(Budget.Decide(Existing(), new RestoreBudget(Guid.NewGuid(), Owner, BudgetId), Now));
    }

    [Fact]
    public void AnArchivedBudgetRejectsWrites()
    {
        var archived = Existing(archived: true);

        Assert.Equal("This budget is archived.",
            Rejection(archived, new RenameBudget(Guid.NewGuid(), Owner, BudgetId, "New")));
        Assert.Equal("This budget is archived.",
            Rejection(archived, new AddCategory(Guid.NewGuid(), Owner, BudgetId, CategoryKind.Expense, "Hobby")));
    }

    [Fact]
    public void AddingACategoryAppendsItToItsKind()
    {
        var budget = Existing();

        budget.ApplyAll(Budget.Decide(budget,
            new AddCategory(Guid.NewGuid(), Owner, BudgetId, CategoryKind.Expense, " Hobby "), Now));

        Assert.Equal("Hobby", budget.ExpenseCategories[^1]);
        Assert.DoesNotContain("Hobby", budget.IncomeCategories);
    }

    [Fact]
    public void AddingAnExistingCategoryInAnyCaseEmitsNothing() =>
        Assert.Empty(Budget.Decide(Existing(),
            new AddCategory(Guid.NewGuid(), Owner, BudgetId, CategoryKind.Expense, "eating out"), Now));

    [Fact]
    public void TheSameNameMayExistInBothKinds()
    {
        var budget = Existing();

        Assert.Single(Budget.Decide(budget,
            new AddCategory(Guid.NewGuid(), Owner, BudgetId, CategoryKind.Income, "Bike"), Now));
    }

    [Theory]
    [MemberData(nameof(ContentCommands))]
    public void AMissingOrForeignBudgetIsIndistinguishable(ICommand command)
    {
        Assert.Equal("That budget does not exist.", Rejection(null, command));
        Assert.Equal("That budget does not exist.", Rejection(Existing(), As(Stranger, command)));
    }

    public static TheoryData<ICommand> ContentCommands => new()
    {
        new RenameBudget(Guid.NewGuid(), Owner, BudgetId, "X"),
        new ArchiveBudget(Guid.NewGuid(), Owner, BudgetId),
        new RestoreBudget(Guid.NewGuid(), Owner, BudgetId),
        new AddCategory(Guid.NewGuid(), Owner, BudgetId, CategoryKind.Expense, "X")
    };

    static ICommand As(Guid actor, ICommand command) => command switch
    {
        RenameBudget rename => rename with { UserId = actor },
        ArchiveBudget archive => archive with { UserId = actor },
        RestoreBudget restore => restore with { UserId = actor },
        AddCategory add => add with { UserId = actor },
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
