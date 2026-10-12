using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record RenameCategory(Guid CommandId, Guid UserId, Guid BudgetId, CategoryKind Kind, string From, string To)
    : ICommand;
