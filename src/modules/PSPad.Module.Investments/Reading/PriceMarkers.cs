using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public static class PriceMarkers
{
    public static IReadOnlyList<PriceMarker> For(string ticker, IEnumerable<InvestmentOperation> operations, TimeZoneInfo zone) =>
        LiveOperations.Of(operations)
            .Where(operation => operation.Ticker == ticker)
            .Select(operation => KindOf(operation.Kind) is { } kind
                ? new PriceMarker(LiveOperations.DayOf(operation, zone), kind, operation.Price, operation.Quantity,
                    operation.Money.Amount, operation.Id)
                : null)
            .OfType<PriceMarker>()
            .ToList();

    static MarkerKind? KindOf(OperationKind kind) => kind switch
    {
        OperationKind.Buy => MarkerKind.Buy,
        OperationKind.Sell => MarkerKind.Sell,
        OperationKind.Dividend => MarkerKind.Dividend,
        _ => null
    };
}
