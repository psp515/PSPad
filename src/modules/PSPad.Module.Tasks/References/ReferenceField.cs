namespace PSPad.Module.Tasks.References;

public sealed record ReferenceField(Guid Id, string Label, string Value, string? Display, int Position);
