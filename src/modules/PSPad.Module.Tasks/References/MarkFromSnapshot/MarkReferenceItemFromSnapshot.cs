using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record MarkReferenceItemFromSnapshot(
    Guid CommandId, Guid UserId, Guid ItemId, Guid SnapshotId, bool Marked) : IServerOnlyCommand;
