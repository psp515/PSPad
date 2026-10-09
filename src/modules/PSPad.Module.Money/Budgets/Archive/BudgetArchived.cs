using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetArchived(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
