using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record MergeCategory(Guid CommandId, Guid UserId, Guid BudgetId, CategoryKind Kind, string From, string Into)
    : ICommand;
