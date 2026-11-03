namespace PSPad.Module.Investments.Import;

public sealed record XtbClosedPosition(
    string PositionId, string Ticker, string? Instrument, string? Product, decimal Volume, decimal? OpenPrice,
    DateTimeOffset? OpenTime, string? Comment, string? Type = null, string? Category = null);
