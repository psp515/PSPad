using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceFieldEdited(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid FieldId, string Label, string Value, string? Display)
    : DomainEvent(AggregateId, UserId, At);
