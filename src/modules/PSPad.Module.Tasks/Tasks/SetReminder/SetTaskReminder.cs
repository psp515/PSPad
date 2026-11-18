using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskReminder(Guid CommandId, Guid UserId, Guid TaskId, TaskReminder? Reminder)
    : ICommand;
