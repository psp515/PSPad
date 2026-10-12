using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record ShareBudget(Guid CommandId, Guid UserId, Guid BudgetId, string Token, string Code, string OwnerName)
    : IServerOnlyCommand;
