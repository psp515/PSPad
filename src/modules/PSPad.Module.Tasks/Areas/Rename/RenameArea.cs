using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record RenameArea(Guid CommandId, Guid UserId, Guid AreaId, string Name) : ICommand;
