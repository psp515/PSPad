using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StarTask(Guid CommandId, Guid UserId, Guid TaskId, bool Starred) : ICommand;
