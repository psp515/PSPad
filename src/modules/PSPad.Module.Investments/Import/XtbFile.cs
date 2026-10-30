namespace PSPad.Module.Investments.Import;

public sealed record XtbFile(
    string AccountNumber, string? Product, IReadOnlyList<XtbRow> Rows, IReadOnlyList<XtbOpenPosition>? OpenPositions);
