namespace PSPad.Module.Investments.Reading;

public sealed record ClosedResult(Guid SellId, string Ticker, decimal Proceeds, decimal Cost)
{
    public decimal Result => Proceeds - Cost;
}
