using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record CreateTaskList(
    Guid CommandId, Guid UserId, Guid ListId, Guid AreaId, string Name, int Position) : ICommand;
