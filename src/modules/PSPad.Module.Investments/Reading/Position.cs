namespace PSPad.Module.Investments.Reading;

public sealed record Position(
    Guid AccountId, string Ticker, decimal Quantity, decimal Cost, decimal AveragePrice, decimal? LastClose,
    DateOnly? PriceDay, decimal Value, decimal Unrealized, IReadOnlyList<Lot> Lots, PositionWarning Warning,
    decimal OversoldBy);
