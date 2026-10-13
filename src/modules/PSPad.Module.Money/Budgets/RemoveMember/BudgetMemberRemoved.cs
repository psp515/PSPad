using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetMemberRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid MemberId)
    : DomainEvent(AggregateId, UserId, At);
