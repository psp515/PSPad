using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemMovedToList(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId)
    : DomainEvent(AggregateId, UserId, At);
