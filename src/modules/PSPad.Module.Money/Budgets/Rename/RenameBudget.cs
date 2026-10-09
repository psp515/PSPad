using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record RenameBudget(Guid CommandId, Guid UserId, Guid BudgetId, string Name) : ICommand;
