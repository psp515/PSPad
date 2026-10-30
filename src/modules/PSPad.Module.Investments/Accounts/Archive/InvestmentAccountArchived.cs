using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record InvestmentAccountArchived(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
