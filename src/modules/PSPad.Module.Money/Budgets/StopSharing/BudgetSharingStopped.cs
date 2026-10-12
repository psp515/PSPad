using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetSharingStopped(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
