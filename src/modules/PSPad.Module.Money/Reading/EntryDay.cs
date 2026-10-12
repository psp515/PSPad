using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Reading;

public sealed record EntryDay(DateOnly Date, IReadOnlyList<MoneyEntry> Entries)
{
    public decimal Net => Entries.Sum(entry => entry.Kind == CategoryKind.Income ? entry.Money.InPln : -entry.Money.InPln);
}
