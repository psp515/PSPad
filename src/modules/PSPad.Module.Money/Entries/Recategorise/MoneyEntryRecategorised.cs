using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record MoneyEntryRecategorised(Guid AggregateId, Guid UserId, DateTimeOffset At, string Category)
    : DomainEvent(AggregateId, UserId, At);
