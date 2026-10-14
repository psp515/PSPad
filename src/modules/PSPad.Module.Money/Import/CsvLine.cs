namespace PSPad.Module.Money.Import;

public sealed record CsvLine(int Number, IReadOnlyList<string> Fields);
