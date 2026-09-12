using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record DeleteArea(Guid CommandId, Guid UserId, Guid AreaId) : ICommand;
