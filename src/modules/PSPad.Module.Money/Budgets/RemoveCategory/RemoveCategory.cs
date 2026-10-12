using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record RemoveCategory(Guid CommandId, Guid UserId, Guid BudgetId, CategoryKind Kind, string Name) : ICommand;
