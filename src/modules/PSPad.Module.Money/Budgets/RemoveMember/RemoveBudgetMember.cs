using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record RemoveBudgetMember(Guid CommandId, Guid UserId, Guid BudgetId, Guid MemberId) : IServerOnlyCommand;
