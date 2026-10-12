using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Reading;

public sealed record CategoryTotal(string Category, decimal Pln)
{
    public static CategoryTotal[] RankingOf(IEnumerable<MoneyEntry> expenses) =>
    [
        .. expenses
            .GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CategoryTotal(group.First().Category, group.Sum(entry => entry.Money.InPln)))
            .OrderByDescending(total => total.Pln)
            .ThenBy(total => total.Category, StringComparer.OrdinalIgnoreCase)
    ];
}
