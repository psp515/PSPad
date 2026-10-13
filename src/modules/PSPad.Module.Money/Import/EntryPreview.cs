namespace PSPad.Module.Money.Import;

public sealed record EntryPreview(int Line, ParsedEntryRow? Row, Money? Money, ImportStatus Status, string? Error);
