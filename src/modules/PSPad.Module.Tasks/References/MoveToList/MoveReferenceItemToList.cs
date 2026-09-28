using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record MoveReferenceItemToList(Guid CommandId, Guid UserId, Guid ItemId, Guid ListId) : ICommand;
