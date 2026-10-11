namespace PSPad.Module.Money.Reading;

public sealed record CategorySeries(string Category, IReadOnlyList<decimal> PerMonth);
