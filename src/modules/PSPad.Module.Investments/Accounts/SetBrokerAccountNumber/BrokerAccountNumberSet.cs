using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record BrokerAccountNumberSet(Guid AggregateId, Guid UserId, DateTimeOffset At, string Number)
    : DomainEvent(AggregateId, UserId, At);
