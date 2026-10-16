namespace PSPad.Module.Money.Reading;

public sealed record Insight(InsightKind Kind, string Category, decimal Amount, decimal Usual, int Day);
