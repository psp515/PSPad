namespace PSPad.Module.Investments.Import;

public sealed record XtbOpenPosition(
    string PositionId, string Ticker, decimal Volume, decimal? OpenPrice = null, DateTimeOffset? OpenTime = null,
    string? Product = null, string? Instrument = null, string? Type = null, string? Category = null);
