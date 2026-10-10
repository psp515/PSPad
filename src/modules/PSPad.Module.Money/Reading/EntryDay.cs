using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Reading;

public sealed record EntryDay(DateOnly Date, IReadOnlyList<MoneyEntry> Entries);
