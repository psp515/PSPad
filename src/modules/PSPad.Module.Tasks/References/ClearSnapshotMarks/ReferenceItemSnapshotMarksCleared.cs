using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemSnapshotMarksCleared(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
