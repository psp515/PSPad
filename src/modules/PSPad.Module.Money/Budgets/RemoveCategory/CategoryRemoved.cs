using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record CategoryRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, CategoryKind Kind, string Name)
    : DomainEvent(AggregateId, UserId, At);
