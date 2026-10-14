using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record MonthFigure(YearMonth Month, decimal Income, decimal Expenses)
{
    public decimal Net => Income - Expenses;

    public bool HasEntries => Income != 0 || Expenses != 0;
}
