using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

public sealed record ParsedHoldingRow(
    int Line, YearMonth Month, string Name, HoldingType Type, decimal Amount, string? Currency, decimal? Rate);
