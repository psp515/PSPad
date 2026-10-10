using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record RecordExpense(
    Guid CommandId, Guid UserId, Guid EntryId, Guid BudgetId, string Name, string Category, Money Money,
    DateOnly Date, string? Note) : ICommand;
