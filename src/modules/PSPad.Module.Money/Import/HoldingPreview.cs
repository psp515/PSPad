namespace PSPad.Module.Money.Import;

public sealed record HoldingPreview(int Line, ParsedHoldingRow? Row, Money? Money, ImportStatus Status, string? Error);
