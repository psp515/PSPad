namespace PSPad.Module.Money.Import;

public sealed record CsvParse<TRow>(IReadOnlyList<TRow> Rows, IReadOnlyList<CsvRowError> Errors, string? FileError)
{
    public static CsvParse<TRow> Failed(string error) => new([], [], error);
}
