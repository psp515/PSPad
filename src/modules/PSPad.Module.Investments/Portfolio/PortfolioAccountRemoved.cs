using PSPad.Abstractions;

namespace PSPad.Module.Investments.Portfolio;

public sealed record PortfolioAccountRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid AccountId)
    : DomainEvent(AggregateId, UserId, At);
