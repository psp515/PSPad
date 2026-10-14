using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.App.State;

public sealed record EntryFilters(EntryFilter Kind, string? Category, string Search, EntrySort Sort)
{
    public static readonly EntryFilters None = new(EntryFilter.All, null, "", EntrySort.EntryDate);

    public bool Shows(MoneyEntry entry) =>
        KindMatches(entry) && CategoryMatches(entry) && SearchMatches(entry);

    bool KindMatches(MoneyEntry entry) => Kind switch
    {
        EntryFilter.Expenses => entry.Kind == CategoryKind.Expense,
        EntryFilter.Income => entry.Kind == CategoryKind.Income,
        _ => true
    };

    bool CategoryMatches(MoneyEntry entry) =>
        Category is null || string.Equals(entry.Category, Category, StringComparison.OrdinalIgnoreCase);

    bool SearchMatches(MoneyEntry entry)
    {
        var text = Search.Trim();
        return text.Length == 0
               || entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
               || entry.Note?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
    }
}
