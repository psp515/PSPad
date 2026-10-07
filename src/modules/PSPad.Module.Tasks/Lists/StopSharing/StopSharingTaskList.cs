using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record StopSharingTaskList(Guid CommandId, Guid UserId, Guid ListId) : ICommand;
