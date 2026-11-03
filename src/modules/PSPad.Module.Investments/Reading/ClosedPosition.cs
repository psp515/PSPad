namespace PSPad.Module.Investments.Reading;

public sealed record ClosedPosition(
    string Ticker, decimal Quantity, decimal Cost, decimal Proceeds, DateTimeOffset? FirstBought, DateTimeOffset LastSold,
    IReadOnlyList<ClosedLot> Lots)
{
    public decimal Result => Proceeds - Cost;

    public decimal? ResultShare => Cost > 0 && !OnlyReceived ? Result / Cost : null;

    bool OnlyReceived => Lots.Count > 0 && Lots.All(lot => lot.Received);
}
