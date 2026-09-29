using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
