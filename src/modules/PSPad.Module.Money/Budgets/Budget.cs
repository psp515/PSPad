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

            case RenameCategory renameCategory:
                var renamingCategory = BudgetAccess.Writable(BudgetAccess.To(budget, renameCategory.UserId));
                var renameList = renamingCategory.CategoriesOf(renameCategory.Kind);
                var source = RequireCategory(renameList, renameCategory.From);
                var target = CategoryName.Normalize(renameCategory.To);
                var clash = CategoryName.IndexIn(renameList, target);
                if (clash >= 0 && clash != source)
                {
                    throw new DomainRejectedException("That category already exists. Merge them instead.");
                }

                return renameList[source] == target
                    ? []
                    : [new CategoryRenamed(renamingCategory.Id, renamingCategory.UserId, at, renameCategory.Kind,
                        renameList[source], target)];

            case MergeCategory mergeCategory:
                var merging = BudgetAccess.Writable(BudgetAccess.To(budget, mergeCategory.UserId));
                var mergeList = merging.CategoriesOf(mergeCategory.Kind);
                var mergeFrom = RequireCategory(mergeList, mergeCategory.From);
                var mergeInto = RequireCategory(mergeList, mergeCategory.Into);
                return mergeFrom == mergeInto
                    ? throw new DomainRejectedException("A category cannot be merged into itself.")
                    : [new CategoriesMerged(merging.Id, merging.UserId, at, mergeCategory.Kind, mergeList[mergeFrom], mergeList[mergeInto])];

            case RemoveCategory removeCategory:
                var removing = BudgetAccess.Writable(BudgetAccess.To(budget, removeCategory.UserId));
                var removeList = removing.CategoriesOf(removeCategory.Kind);
                return [new CategoryRemoved(removing.Id, removing.UserId, at, removeCategory.Kind,
                    removeList[RequireCategory(removeList, removeCategory.Name)])];

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
                ListOf(added.Kind).Add(added.Name);
                break;
            case CategoryRenamed renamed:
                var renamedList = ListOf(renamed.Kind);
                renamedList[CategoryName.IndexIn(renamedList, renamed.From)] = renamed.To;
                break;
            case CategoriesMerged merged:
                var mergedList = ListOf(merged.Kind);
                mergedList.RemoveAt(CategoryName.IndexIn(mergedList, merged.From));
                break;
            case CategoryRemoved removed:
                var removedList = ListOf(removed.Kind);
                removedList.RemoveAt(CategoryName.IndexIn(removedList, removed.Name));
                break;
        }
    }

    List<string> ListOf(CategoryKind kind) => kind == CategoryKind.Expense ? _expenseCategories : _incomeCategories;

    static int RequireCategory(IReadOnlyList<string> names, string name)
    {
        var index = CategoryName.IndexIn(names, name);
        return index < 0 ? throw new DomainRejectedException("That category does not exist.") : index;
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
