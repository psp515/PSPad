using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record CreateTaskList(
    Guid CommandId, Guid UserId, Guid ListId, Guid AreaId, string Name, int Position) : ICommand;

public sealed record RenameTaskList(Guid CommandId, Guid UserId, Guid ListId, string Name) : ICommand;

public sealed record MoveTaskListToArea(Guid CommandId, Guid UserId, Guid ListId, Guid AreaId) : ICommand;

public sealed record DeleteTaskList(Guid CommandId, Guid UserId, Guid ListId) : ICommand;
