using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemDescriptionSet(Guid AggregateId, Guid UserId, DateTimeOffset At, string Description)
    : DomainEvent(AggregateId, UserId, At);
