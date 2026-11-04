using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record InvestmentAccountRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
