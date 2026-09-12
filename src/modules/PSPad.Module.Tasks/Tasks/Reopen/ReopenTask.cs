using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record ReopenTask(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;
