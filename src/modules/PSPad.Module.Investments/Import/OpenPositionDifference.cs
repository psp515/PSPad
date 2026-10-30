namespace PSPad.Module.Investments.Import;

public sealed record OpenPositionDifference(string PositionId, string Ticker, decimal FileVolume, decimal ImportedVolume);
