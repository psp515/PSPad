using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

internal static class ImportMoney
{
    public static (Money? Money, RateRequest? Missing, string? Failure) Resolve(
        decimal amount, string currency, decimal? rate, DateOnly ownDate, RateBook rates)
    {
        if (currency == Currencies.Pln || rate is not null)
        {
            return (Money.Require(new Money(amount, currency, rate ?? 1m, ownDate), ownDate), null, null);
        }

        var request = new RateRequest(currency, ownDate);
        return rates.Known.TryGetValue(request, out var known)
            ? (Money.Require(new Money(amount, currency, known.Rate, known.EffectiveDate), ownDate), null, null)
            : (null, request, rates.Failed.GetValueOrDefault(request));
    }
}
