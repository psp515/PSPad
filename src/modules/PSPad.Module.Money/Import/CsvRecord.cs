namespace PSPad.Module.Money.Import;

internal sealed class CsvRecord(CsvLine line, IReadOnlyDictionary<string, int> columns)
{
    public int Line => line.Number;

    public string Text(string column) =>
        columns.TryGetValue(column, out var index) && index < line.Fields.Count ? line.Fields[index].Trim() : "";
}
