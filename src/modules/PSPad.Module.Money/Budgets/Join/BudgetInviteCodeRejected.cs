using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetInviteCodeRejected(Guid AggregateId, Guid UserId, DateTimeOffset At, int WrongCodes)
    : DomainEvent(AggregateId, UserId, At);
