using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed class Budget : Aggregate
{
    public const int MaxNameLength = 80;

    public static IReadOnlyList<string> SeedExpenseCategories { get; } =
        ["Home", "Food", "Eating Out", "PC", "Bike", "Car", "Gifts", "Clothes"];

    public static IReadOnlyList<string> SeedIncomeCategories { get; } =
        ["Salary", "Freelance", "Interest", "Gifts", "Other"];

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public DateTimeOffset CreatedAt { get; private set; }

    [JsonInclude]
    public DateTimeOffset? ArchivedAt { get; private set; }

    [JsonInclude]
    List<string> _expenseCategories = [];

    [JsonInclude]
    List<string> _incomeCategories = [];

    public IReadOnlyList<string> ExpenseCategories => _expenseCategories;

    public IReadOnlyList<string> IncomeCategories => _incomeCategories;

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyList<string> CategoriesOf(CategoryKind kind) =>
        kind == CategoryKind.Expense ? _expenseCategories : _incomeCategories;

    public static IReadOnlyList<DomainEvent> Decide(Budget? budget, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateBudget create:
                if (budget is not null)
                {
                    throw new DomainRejectedException("That budget already exists.");
                }

                return [new BudgetCreated(create.BudgetId, create.UserId, at, RequireName(create.Name),
                    SeedExpenseCategories, SeedIncomeCategories)];

            case RenameBudget rename:
                var renaming = BudgetAccess.Writable(BudgetAccess.To(budget, rename.UserId));
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new BudgetRenamed(renaming.Id, renaming.UserId, at, name)];

            case ArchiveBudget archive:
                var archiving = BudgetAccess.To(budget, archive.UserId);
                return archiving.IsArchived ? [] : [new BudgetArchived(archiving.Id, archiving.UserId, at)];

            case RestoreBudget restore:
                var restoring = BudgetAccess.To(budget, restore.UserId);
                return restoring.IsArchived ? [new BudgetRestored(restoring.Id, restoring.UserId, at)] : [];

            case AddCategory add:
                var adding = BudgetAccess.Writable(BudgetAccess.To(budget, add.UserId));
                var category = CategoryName.Normalize(add.Name);
                return CategoryName.IndexIn(adding.CategoriesOf(add.Kind), category) >= 0
                    ? []
                    : [new CategoryAdded(adding.Id, adding.UserId, at, add.Kind, category)];

            default:
                throw new DomainRejectedException($"A budget cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case BudgetCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                Name = created.Name;
                CreatedAt = created.At;
                _expenseCategories = [.. created.ExpenseCategories];
                _incomeCategories = [.. created.IncomeCategories];
                break;
            case BudgetRenamed renamed:
                Name = renamed.Name;
                break;
            case BudgetArchived archived:
                ArchivedAt = archived.At;
                break;
            case BudgetRestored:
                ArchivedAt = null;
                break;
            case CategoryAdded added:
                (added.Kind == CategoryKind.Expense ? _expenseCategories : _incomeCategories).Add(added.Name);
                break;
        }
    }

    static string RequireName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRejectedException("A budget needs a name.");
        }

        return trimmed.Length > MaxNameLength
            ? throw new DomainRejectedException($"A budget name is at most {MaxNameLength} characters.")
            : trimmed;
    }
}
