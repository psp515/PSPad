using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record MarkTaskFromSnapshot(
    Guid CommandId, Guid UserId, Guid TaskId, Guid? StepId, Guid SnapshotId, bool Marked) : IServerOnlyCommand;
