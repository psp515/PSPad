using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskSnapshotMarkSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid SnapshotId, Guid? StepId, bool Marked)
    : DomainEvent(AggregateId, UserId, At);
