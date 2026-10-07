using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record LeaveTaskList(Guid CommandId, Guid UserId, Guid ListId) : ICommand;
