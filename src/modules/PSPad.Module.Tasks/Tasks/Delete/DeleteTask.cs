using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record DeleteTask(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;
