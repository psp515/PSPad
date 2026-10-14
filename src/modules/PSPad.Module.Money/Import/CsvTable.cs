namespace PSPad.Module.Money.Import;

public sealed record CsvTable(IReadOnlyList<string> Header, IReadOnlyList<CsvLine> Lines, string? Error)
{
    public static CsvTable Failed(string error) => new([], [], error);
}
