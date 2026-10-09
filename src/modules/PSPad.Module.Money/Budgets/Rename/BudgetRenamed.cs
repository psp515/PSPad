using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
