using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record MoveTaskToList(Guid CommandId, Guid UserId, Guid TaskId, Guid ListId) : ICommand;
