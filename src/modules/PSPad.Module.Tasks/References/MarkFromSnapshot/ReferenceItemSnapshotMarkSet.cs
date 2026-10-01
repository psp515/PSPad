using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemSnapshotMarkSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid SnapshotId, bool Marked)
    : DomainEvent(AggregateId, UserId, At);
