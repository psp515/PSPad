using PSPad.Abstractions;

namespace PSPad.Module.Money.Preferences;

public sealed record DefaultCurrencySet(Guid AggregateId, Guid UserId, DateTimeOffset At, string Currency)
    : DomainEvent(AggregateId, UserId, At);
