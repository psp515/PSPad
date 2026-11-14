using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.App.State;

public sealed class PurchaseReturns(IEnumerable<MoneyEntry> entries)
{
    readonly ILookup<Guid, MoneyEntry> _byPurchase = entries
        .Where(entry => !entry.Deleted && entry.ReturnOf is not null)
        .ToLookup(entry => entry.ReturnOf!.Value);

    public ReturnState? Of(MoneyEntry entry) =>
        entry.Kind == CategoryKind.Expense && !entry.IsReturn && _byPurchase.Contains(entry.Id)
            ? ReturnState.Of(entry, _byPurchase[entry.Id])
            : null;
}
