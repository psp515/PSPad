namespace PSPad.Contracts;

public sealed record NbpRateView(string Currency, decimal Rate, DateOnly EffectiveDate);
