using PSPad.Abstractions;
using PSPad.Module.Tasks.Recurrence;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskRecurrence(Guid CommandId, Guid UserId, Guid TaskId, RecurrenceRule? Rule)
    : ICommand;
