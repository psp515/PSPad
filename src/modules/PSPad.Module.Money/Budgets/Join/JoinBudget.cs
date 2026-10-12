using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record JoinBudget(Guid CommandId, Guid UserId, Guid BudgetId, string Token, string Code, string DisplayName)
    : IServerOnlyCommand;
