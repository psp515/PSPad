using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record BalanceTotals(BalanceSnapshot? Snapshot, BalanceSnapshot? Previous)
{
    public decimal NetWorth => Snapshot is null ? 0m : NetWorthOf(Snapshot);

    public decimal? Change => Snapshot is null || Previous is null ? null : NetWorth - NetWorthOf(Previous);

    public static BalanceTotals For(IEnumerable<BalanceSnapshot> snapshots, YearMonth month)
    {
        var live = snapshots.Where(snapshot => !snapshot.Deleted).ToArray();
        return new BalanceTotals(
            live.FirstOrDefault(snapshot => snapshot.Month == month),
            Latest(live, month.Previous()));
    }

    public static BalanceSnapshot? Latest(IEnumerable<BalanceSnapshot> snapshots, YearMonth onOrBefore) =>
        snapshots
            .Where(snapshot => !snapshot.Deleted && snapshot.Month <= onOrBefore)
            .OrderByDescending(snapshot => snapshot.Month)
            .FirstOrDefault();

    public static decimal NetWorthOf(BalanceSnapshot snapshot) =>
        snapshot.Holdings.Sum(holding => holding.Money.InPln);

    public static decimal? ValueToday(Holding holding, IReadOnlyDictionary<string, decimal> rates) =>
        holding.Money.Currency == Currencies.Pln
            ? holding.Money.InPln
            : rates.TryGetValue(holding.Money.Currency, out var rate)
                ? (holding.Money with { RateToPln = rate }).InPln
                : null;

    public static decimal? AtRates(BalanceSnapshot snapshot, IReadOnlyDictionary<string, decimal> rates)
    {
        var values = snapshot.Holdings.Select(holding => ValueToday(holding, rates)).ToArray();
        return values.Any(value => value is null) ? null : values.Sum(value => value!.Value);
    }
}
