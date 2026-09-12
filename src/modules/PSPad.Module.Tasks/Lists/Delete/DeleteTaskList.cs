using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record DeleteTaskList(Guid CommandId, Guid UserId, Guid ListId) : ICommand;
