using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record InvestmentAccountCreated(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name, string Currency, Broker Broker, string? BrokerAccountNumber)
    : DomainEvent(AggregateId, UserId, At);
