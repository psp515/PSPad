using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record CreateReferenceItem(Guid CommandId, Guid UserId, Guid ItemId, Guid ListId, string Name, int Position)
    : ICommand;
