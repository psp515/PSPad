using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record CumulativeMonth(YearMonth Month, decimal Income, decimal Expenses);
