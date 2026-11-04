using PSPad.Abstractions;

namespace PSPad.Module.Investments.Operations;

public sealed record InvestmentOperationRestored(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
