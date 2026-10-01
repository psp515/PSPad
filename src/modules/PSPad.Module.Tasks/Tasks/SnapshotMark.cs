namespace PSPad.Module.Tasks.Tasks;

public sealed record SnapshotMark(Guid SnapshotId, Guid? StepId, DateTimeOffset MarkedAt);
