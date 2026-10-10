using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record MoneyEntryEdited(
    Guid AggregateId, Guid UserId, DateTimeOffset At, string Name, string Category, Money Money, DateOnly Date,
    string? Note) : DomainEvent(AggregateId, UserId, At);
