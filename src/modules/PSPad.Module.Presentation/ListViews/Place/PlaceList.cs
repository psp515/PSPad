using PSPad.Abstractions;

namespace PSPad.Module.Presentation.ListViews;

public sealed record PlaceList(Guid CommandId, Guid UserId, Guid ListId, Guid? AreaId) : ICommand;
