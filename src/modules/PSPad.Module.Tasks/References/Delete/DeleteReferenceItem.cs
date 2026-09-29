using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record DeleteReferenceItem(Guid CommandId, Guid UserId, Guid ItemId) : ICommand;
