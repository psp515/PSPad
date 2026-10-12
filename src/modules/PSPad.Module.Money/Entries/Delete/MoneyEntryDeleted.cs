using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record MoneyEntryDeleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
