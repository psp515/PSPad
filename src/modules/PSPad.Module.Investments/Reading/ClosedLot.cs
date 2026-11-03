namespace PSPad.Module.Investments.Reading;

public sealed record ClosedLot(
    string? Key, DateTimeOffset? OpenedAt, decimal? OpenPrice, DateTimeOffset ClosedAt, decimal? ClosePrice, decimal Quantity,
    decimal Cost, decimal Proceeds, bool Received = false)
{
    public decimal Result => Proceeds - Cost;
}
