namespace PSPad.Module.Investments.Reading;

public sealed record Lot(string Key, string Ticker, DateTimeOffset OpenedAt, decimal Quantity, decimal Cost);
