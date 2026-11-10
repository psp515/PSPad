using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record MoneyEntryRelabelled(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name, string Category)
    : DomainEvent(AggregateId, UserId, At);
