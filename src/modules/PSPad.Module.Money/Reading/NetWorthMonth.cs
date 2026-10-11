using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record NetWorthMonth(YearMonth Month, decimal? Cash, decimal? Savings, decimal? Investment)
{
    public bool IsGap => Cash is null;

    public decimal? Total => Cash + Savings + Investment;

    public static NetWorthMonth Of(YearMonth month, BalanceSnapshot? snapshot) =>
        snapshot is null
            ? new NetWorthMonth(month, null, null, null)
            : new NetWorthMonth(month,
                SumOf(snapshot, HoldingType.Cash), SumOf(snapshot, HoldingType.Savings), SumOf(snapshot, HoldingType.Investment));

    static decimal SumOf(BalanceSnapshot snapshot, HoldingType type) =>
        snapshot.Holdings.Where(holding => holding.Type == type).Sum(holding => holding.Money.InPln);
}
