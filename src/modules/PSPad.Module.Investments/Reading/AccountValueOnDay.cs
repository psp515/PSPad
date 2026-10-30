using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public sealed record AccountValueOnDay(
    Guid AccountId, decimal Value, string Currency, IReadOnlyDictionary<string, DateOnly?> PriceDays, bool AnyStale,
    bool AnyNoPrice)
{
    public static AccountValueOnDay For(
        InvestmentAccount account, IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyDictionary<string, PriceHistory> pricesByTicker, FxBook fx, DateOnly day, TimeZoneInfo zone)
    {
        var live = LiveOperations.Until(operations, day, zone);
        var (open, _, oversold) = Lots.Of(live);
        var held = Valuation.Positions(account.Id, account.Currency, open, oversold, pricesByTicker, fx, day)
            .Where(position => position.Quantity > 0)
            .ToList();
        var value = live.Sum(operation => operation.SignedAmount) + held.Sum(position => position.Value);

        return new AccountValueOnDay(account.Id, value, account.Currency,
            held.ToDictionary(position => position.Ticker, position => position.PriceDay),
            held.Any(position => position.Warning != PositionWarning.NoPrice && position.PriceDay < day),
            held.Any(position => position.Warning == PositionWarning.NoPrice));
    }
}
