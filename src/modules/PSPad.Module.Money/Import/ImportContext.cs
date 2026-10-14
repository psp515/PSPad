using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Import;

public sealed record ImportContext(
    Budget Budget, IReadOnlyList<MoneyEntry> Entries, IReadOnlyList<BalanceSnapshot> Snapshots, string DefaultCurrency);
