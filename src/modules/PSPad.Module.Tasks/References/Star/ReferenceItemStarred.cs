using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemStarred(Guid AggregateId, Guid UserId, DateTimeOffset At, bool Starred)
    : DomainEvent(AggregateId, UserId, At);
