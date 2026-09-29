using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceFieldRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid FieldId)
    : DomainEvent(AggregateId, UserId, At);
