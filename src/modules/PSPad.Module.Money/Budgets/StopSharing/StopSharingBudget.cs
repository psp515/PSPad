using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record StopSharingBudget(Guid CommandId, Guid UserId, Guid BudgetId) : IServerOnlyCommand;
