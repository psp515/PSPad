using PSPad.Abstractions;

namespace PSPad.Module.Investments.Instruments;

public sealed record InstrumentEnsured(Guid AggregateId, Guid UserId, DateTimeOffset At, string Ticker, string Name, string QuoteSymbol)
    : DomainEvent(AggregateId, UserId, At);
