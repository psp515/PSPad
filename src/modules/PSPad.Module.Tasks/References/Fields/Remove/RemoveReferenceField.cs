using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record RemoveReferenceField(Guid CommandId, Guid UserId, Guid ItemId, Guid FieldId) : ICommand;
