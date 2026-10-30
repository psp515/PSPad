namespace PSPad.Module.Investments.Reading;

public sealed record PriceMarker(
    DateOnly Day, MarkerKind Kind, decimal? Price, decimal? Quantity, decimal Amount, Guid OperationId);
