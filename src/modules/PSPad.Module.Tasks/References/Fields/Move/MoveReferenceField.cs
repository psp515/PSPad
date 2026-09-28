using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record MoveReferenceField(Guid CommandId, Guid UserId, Guid ItemId, Guid FieldId, int ToIndex) : ICommand;
