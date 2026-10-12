using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record CategoryRenamed(
    Guid AggregateId, Guid UserId, DateTimeOffset At, CategoryKind Kind, string From, string To)
    : DomainEvent(AggregateId, UserId, At);
