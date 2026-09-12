using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record CompleteTask(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;
