using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

internal static class ImportMoney
{
    public static (Money? Money, RateRequest? Missing, string? Failure) Resolve(
        decimal amount, string currency, decimal? rate, DateOnly ownDate, RateBook rates)
    {
        if (currency == Currencies.Pln || rate is not null)
        {
            return Required(new Money(amount, currency, rate ?? 1m, ownDate), ownDate);
        }

        var request = new RateRequest(currency, ownDate);
        return rates.Known.TryGetValue(request, out var known)
            ? Required(new Money(amount, currency, known.Rate, known.EffectiveDate), ownDate)
            : (null, request, rates.Failed.GetValueOrDefault(request));
    }

    static (Money? Money, RateRequest? Missing, string? Failure) Required(Money money, DateOnly ownDate)
    {
        try
        {
            return (Money.Require(money, ownDate), null, null);
        }
        catch (DomainRejectedException rejected)
        {
            return (null, null, rejected.Message);
        }
    }
}
