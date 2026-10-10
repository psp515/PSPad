using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public sealed class MoneyEntry : Aggregate
{
    public const int MaxNameLength = 120;

    [JsonInclude]
    public Guid BudgetId { get; private set; }

    [JsonInclude]
    public CategoryKind Kind { get; private set; }

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public string Category { get; private set; } = "";

    [JsonInclude]
    public Money Money { get; private set; } = default!;

    [JsonInclude]
    public DateOnly Date { get; private set; }

    [JsonInclude]
    public string? Note { get; private set; }

    [JsonInclude]
    public DateTimeOffset RecordedAt { get; private set; }

    public bool Uses(CategoryKind kind, string category) =>
        !Deleted && Kind == kind && string.Equals(Category, category.Trim(), StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<DomainEvent> Relabel(MoneyEntry entry, string category, DateTimeOffset at) =>
        entry.Category == category ? [] : [new MoneyEntryRecategorised(entry.Id, entry.UserId, at, category)];

    public static IReadOnlyList<DomainEvent> Decide(MoneyEntry? entry, Budget? budget, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case RecordExpense expense:
                var expenseBudget = BudgetAccess.Writable(BudgetAccess.To(budget, expense.UserId));
                RequireNew(entry);
                return [new ExpenseRecorded(expense.EntryId, expenseBudget.UserId, at, expenseBudget.Id,
                    RequireName(expense.Name), CategoryName.Resolve(expenseBudget.ExpenseCategories, expense.Category),
                    Money.Require(expense.Money, expense.Date), expense.Date, NoteOf(expense.Note))];

            case RecordIncome income:
                var incomeBudget = BudgetAccess.Writable(BudgetAccess.To(budget, income.UserId));
                RequireNew(entry);
                return [new IncomeRecorded(income.EntryId, incomeBudget.UserId, at, incomeBudget.Id,
                    RequireName(income.Name), CategoryName.Resolve(incomeBudget.IncomeCategories, income.Category),
                    Money.Require(income.Money, income.Date), income.Date, NoteOf(income.Note))];

            case EditEntry edit:
                var editing = RequireLive(entry);
                var editBudget = BudgetAccess.Writable(BudgetAccess.To(budget, edit.UserId));
                var name = RequireName(edit.Name);
                var category = CategoryName.Resolve(editBudget.CategoriesOf(editing.Kind), edit.Category);
                var money = Money.Require(edit.Money, edit.Date);
                var note = NoteOf(edit.Note);
                return editing.Name == name && editing.Category == category && editing.Money == money &&
                       editing.Date == edit.Date && editing.Note == note
                    ? []
                    : [new MoneyEntryEdited(editing.Id, editing.UserId, at, name, category, money, edit.Date, note)];

            case DeleteEntry delete:
                var deleting = entry ?? throw new DomainRejectedException("That entry does not exist.");
                BudgetAccess.Writable(BudgetAccess.To(budget, delete.UserId));
                return deleting.Deleted ? [] : [new MoneyEntryDeleted(deleting.Id, deleting.UserId, at)];

            default:
                throw new DomainRejectedException($"A money entry cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case ExpenseRecorded expense:
                Recorded(expense.AggregateId, expense.UserId, expense.At, CategoryKind.Expense, expense.BudgetId,
                    expense.Name, expense.Category, expense.Money, expense.Date, expense.Note);
                break;
            case IncomeRecorded income:
                Recorded(income.AggregateId, income.UserId, income.At, CategoryKind.Income, income.BudgetId,
                    income.Name, income.Category, income.Money, income.Date, income.Note);
                break;
            case MoneyEntryEdited edited:
                Name = edited.Name;
                Category = edited.Category;
                Money = edited.Money;
                Date = edited.Date;
                Note = edited.Note;
                break;
            case MoneyEntryRecategorised recategorised:
                Category = recategorised.Category;
                break;
            case MoneyEntryDeleted:
                Deleted = true;
                break;
        }
    }

    void Recorded(Guid id, Guid userId, DateTimeOffset at, CategoryKind kind, Guid budgetId, string name,
        string category, Money money, DateOnly date, string? note)
    {
        Id = id;
        UserId = userId;
        RecordedAt = at;
        Kind = kind;
        BudgetId = budgetId;
        Name = name;
        Category = category;
        Money = money;
        Date = date;
        Note = note;
    }

    static void RequireNew(MoneyEntry? entry)
    {
        if (entry is not null)
        {
            throw new DomainRejectedException("That entry already exists.");
        }
    }

    static MoneyEntry RequireLive(MoneyEntry? entry) =>
        entry is null || entry.Deleted ? throw new DomainRejectedException("That entry does not exist.") : entry;

    static string RequireName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRejectedException("An entry needs a name.");
        }

        return trimmed.Length > MaxNameLength
            ? throw new DomainRejectedException($"An entry name is at most {MaxNameLength} characters.")
            : trimmed;
    }

    static string? NoteOf(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
