using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskDueDate(Guid CommandId, Guid UserId, Guid TaskId, DateOnly? DueOn) : ICommand;
