using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskTime(Guid CommandId, Guid UserId, Guid TaskId, TaskTime? Time) : ICommand;
