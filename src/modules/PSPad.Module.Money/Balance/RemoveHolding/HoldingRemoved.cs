using PSPad.Abstractions;

namespace PSPad.Module.Money.Balance;

public sealed record HoldingRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid HoldingId)
    : DomainEvent(AggregateId, UserId, At);
