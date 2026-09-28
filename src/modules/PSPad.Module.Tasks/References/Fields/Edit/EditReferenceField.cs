using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record EditReferenceField(
    Guid CommandId, Guid UserId, Guid ItemId, Guid FieldId, string Label, string Value, string? Display)
    : ICommand;
