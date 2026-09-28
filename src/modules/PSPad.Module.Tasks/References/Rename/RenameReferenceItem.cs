using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record RenameReferenceItem(Guid CommandId, Guid UserId, Guid ItemId, string Name) : ICommand;
