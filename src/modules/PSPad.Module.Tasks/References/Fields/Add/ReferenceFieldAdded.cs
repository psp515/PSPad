using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceFieldAdded(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid FieldId, string Label, string Value, string? Display, int Position)
    : DomainEvent(AggregateId, UserId, At);
