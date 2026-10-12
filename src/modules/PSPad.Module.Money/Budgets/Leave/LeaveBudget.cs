using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record LeaveBudget(Guid CommandId, Guid UserId, Guid BudgetId) : IServerOnlyCommand;
