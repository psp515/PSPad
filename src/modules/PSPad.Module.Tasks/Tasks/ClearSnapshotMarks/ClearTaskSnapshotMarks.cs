using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record ClearTaskSnapshotMarks(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;
