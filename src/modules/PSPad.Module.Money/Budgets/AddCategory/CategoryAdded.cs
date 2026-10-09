using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record CategoryAdded(Guid AggregateId, Guid UserId, DateTimeOffset At, CategoryKind Kind, string Name)
    : DomainEvent(AggregateId, UserId, At);
