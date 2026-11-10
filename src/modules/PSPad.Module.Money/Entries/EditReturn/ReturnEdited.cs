using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record ReturnEdited(Guid AggregateId, Guid UserId, DateTimeOffset At, Money Money, DateOnly Date, string? Note)
    : DomainEvent(AggregateId, UserId, At);
