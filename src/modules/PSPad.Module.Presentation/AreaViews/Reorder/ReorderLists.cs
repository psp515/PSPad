using PSPad.Abstractions;

namespace PSPad.Module.Presentation.AreaViews;

public sealed record ReorderLists(
    Guid CommandId,
    Guid UserId,
    Guid AreaId,
    IReadOnlyList<Guid> Order,
    Guid ListId,
    int ToIndex) : ICommand;
