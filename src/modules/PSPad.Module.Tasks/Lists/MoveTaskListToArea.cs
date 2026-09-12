using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record MoveTaskListToArea(Guid CommandId, Guid UserId, Guid ListId, Guid AreaId) : ICommand;
