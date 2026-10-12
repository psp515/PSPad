using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetShared(Guid AggregateId, Guid UserId, DateTimeOffset At, string Token, string Code, string OwnerName)
    : DomainEvent(AggregateId, UserId, At);
