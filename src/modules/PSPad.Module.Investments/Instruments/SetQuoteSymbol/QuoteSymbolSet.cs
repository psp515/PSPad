using PSPad.Abstractions;

namespace PSPad.Module.Investments.Instruments;

public sealed record QuoteSymbolSet(Guid AggregateId, Guid UserId, DateTimeOffset At, string Symbol)
    : DomainEvent(AggregateId, UserId, At);
