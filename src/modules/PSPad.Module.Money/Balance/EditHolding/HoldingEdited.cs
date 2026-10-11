using PSPad.Abstractions;

namespace PSPad.Module.Money.Balance;

public sealed record HoldingEdited(Guid AggregateId, Guid UserId, DateTimeOffset At, Holding Holding)
    : DomainEvent(AggregateId, UserId, At);
