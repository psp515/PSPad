namespace PSPad.Module.Investments.Reading;

public sealed record CurrencyExchange(
    string Currency, decimal BoughtUnits, decimal BoughtPricedUnits, decimal BoughtPln, decimal SoldUnits,
    decimal SoldPricedUnits, decimal SoldPln, decimal? HeldUnits, decimal? HeldPln, decimal? TodayRate, int Count,
    DateOnly First, DateOnly Last)
{
    public decimal? BoughtRate => BoughtPricedUnits > 0 ? Math.Round(BoughtPln / BoughtPricedUnits, 4, MidpointRounding.AwayFromZero) : null;

    public decimal? SoldRate => SoldPricedUnits > 0 ? Math.Round(SoldPln / SoldPricedUnits, 4, MidpointRounding.AwayFromZero) : null;

    public decimal? CurrencyResult => HeldUnits is { } units && HeldPln is { } cost && TodayRate is { } rate
        ? Valuation.Money(units * rate - cost)
        : null;
}
