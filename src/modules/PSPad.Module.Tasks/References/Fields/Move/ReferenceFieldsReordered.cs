using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceFieldsReordered(Guid AggregateId, Guid UserId, DateTimeOffset At, IReadOnlyList<Guid> Order)
    : DomainEvent(AggregateId, UserId, At);
