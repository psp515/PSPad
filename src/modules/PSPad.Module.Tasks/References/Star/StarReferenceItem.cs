using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record StarReferenceItem(Guid CommandId, Guid UserId, Guid ItemId, bool Starred) : ICommand;
