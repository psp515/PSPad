using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record CreateArea(Guid CommandId, Guid UserId, Guid AreaId, string Name, int Position)
    : ICommand;
