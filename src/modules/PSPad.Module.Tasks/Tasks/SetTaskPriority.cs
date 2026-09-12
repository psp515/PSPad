using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskPriority(Guid CommandId, Guid UserId, Guid TaskId, Priority Priority) : ICommand;
