namespace PSPad.Module.Investments.Reading;

internal static class Valuation
{
    public static IReadOnlyList<Position> Positions(
        Guid accountId, string currency, IReadOnlyList<Lot> open, IReadOnlyDictionary<string, decimal> oversold,
        IReadOnlyDictionary<string, PriceHistory> pricesByTicker, FxBook fx, DateOnly day) =>
        open.Select(lot => lot.Ticker).Concat(oversold.Keys).Distinct().Order(StringComparer.Ordinal)
            .Select(ticker => Position(accountId, currency, ticker, open.Where(lot => lot.Ticker == ticker).ToList(),
                oversold.GetValueOrDefault(ticker), pricesByTicker.GetValueOrDefault(ticker), fx, day))
            .ToList();

    public static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    static Position Position(
        Guid accountId, string currency, string ticker, IReadOnlyList<Lot> lots, decimal oversoldBy, PriceHistory? prices,
        FxBook fx, DateOnly day)
    {
        var quantity = lots.Sum(lot => lot.Quantity);
        var cost = lots.Sum(lot => lot.Cost);
        var close = prices?.On(day);
        var converted = prices is not null && close is not null
            ? fx.Convert(quantity * close.Close * prices.UnitFactor, prices.MajorCurrency, currency, close.Date)
            : null;
        var value = converted is { } priced ? Money(priced) : cost;
        var warning = quantity > 0 && converted is null ? PositionWarning.NoPrice
            : oversoldBy > 0 ? PositionWarning.Oversold
            : PositionWarning.None;
        var average = quantity == 0 ? 0m : Math.Round(cost / quantity, 8, MidpointRounding.AwayFromZero);

        return new Position(accountId, ticker, quantity, cost, average, close?.Close, close?.Date, value, value - cost, lots,
            warning, oversoldBy);
    }
}
