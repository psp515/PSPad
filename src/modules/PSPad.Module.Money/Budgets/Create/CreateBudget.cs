using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record CreateBudget(Guid CommandId, Guid UserId, Guid BudgetId, string Name) : ICommand;
