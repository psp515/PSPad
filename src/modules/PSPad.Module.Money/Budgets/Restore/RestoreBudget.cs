using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record RestoreBudget(Guid CommandId, Guid UserId, Guid BudgetId) : ICommand;
