namespace PSPad.Module.Investments.Import;

public sealed record XlsxSheet(string Name, IReadOnlyList<IReadOnlyList<XlsxCell?>> Rows);
