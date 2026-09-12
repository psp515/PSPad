using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record CreateTask(Guid CommandId, Guid UserId, Guid TaskId, Guid ListId, string Name) : ICommand;
