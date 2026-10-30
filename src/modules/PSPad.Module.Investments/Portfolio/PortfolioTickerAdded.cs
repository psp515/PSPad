using PSPad.Abstractions;

namespace PSPad.Module.Investments.Portfolio;

public sealed record PortfolioTickerAdded(Guid AggregateId, Guid UserId, DateTimeOffset At, string Ticker)
    : DomainEvent(AggregateId, UserId, At);
