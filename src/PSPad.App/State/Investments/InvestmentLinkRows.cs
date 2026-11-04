using PSPad.Module.Money;
using PSPad.Module.Money.Values;

namespace PSPad.App.State.Investments;

public static class InvestmentLinkRows
{
    public static IReadOnlyList<LinkRow> Ready(IEnumerable<LinkRow> rows) =>
        [.. rows.Where(row => row.Ticked && !row.Loading && row.Value is { Value: > 0 })];

    public static decimal RateOf(LinkRow row) =>
        row.Account.Currency == Currencies.Pln ? 1m : row.Rate.GetValueOrDefault();

    public static decimal Total(IEnumerable<LinkRow> rows) =>
        Ready(rows).Where(row => RateOf(row) > 0)
            .Sum(row => new Money(row.Value!.Value, row.Account.Currency, RateOf(row), row.RateDate).InPln);

    public static bool CanConfirm(IEnumerable<LinkRow> rows, bool busy)
    {
        var ready = Ready(rows);
        return !busy && ready.Count > 0 && ready.All(row => RateOf(row) > 0);
    }
}
