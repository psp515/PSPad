using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public static class ClosedPositions
{
    public static IReadOnlyList<ClosedPosition> For(IEnumerable<InvestmentOperation> operations)
    {
        var live = LiveOperations.Of(operations);
        var book = new LotBook();
        foreach (var operation in live)
        {
            book.Apply(operation);
        }

        var held = book.Open.Where(lot => lot.Quantity > 0).Select(lot => lot.Ticker).ToHashSet(StringComparer.Ordinal);
        return [.. book.ClosedLots.Where(closed => !held.Contains(closed.Ticker))
            .GroupBy(closed => closed.Ticker, closed => closed.Lot, StringComparer.Ordinal)
            .Select(lots => Of(lots.Key, [.. lots], live))
            .OrderByDescending(closed => closed.LastSold).ThenBy(closed => closed.Ticker, StringComparer.Ordinal)];
    }

    static ClosedPosition Of(string ticker, IReadOnlyList<ClosedLot> lots, IReadOnlyList<InvestmentOperation> live)
    {
        var bought = live.Where(operation => operation.Ticker == ticker
                && operation.Kind is OperationKind.Buy or OperationKind.SharesReceived)
            .Select(operation => (DateTimeOffset?)operation.At)
            .Min();
        return new ClosedPosition(ticker, lots.Sum(lot => lot.Quantity), lots.Sum(lot => lot.Cost), lots.Sum(lot => lot.Proceeds),
            bought, lots.Max(lot => lot.ClosedAt), lots);
    }
}
