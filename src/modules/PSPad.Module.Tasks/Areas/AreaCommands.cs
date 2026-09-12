using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record CreateArea(Guid CommandId, Guid UserId, Guid AreaId, string Name, int Position)
    : ICommand;

public sealed record RenameArea(Guid CommandId, Guid UserId, Guid AreaId, string Name) : ICommand;

public sealed record DeleteArea(Guid CommandId, Guid UserId, Guid AreaId) : ICommand;
