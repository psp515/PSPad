using PSPad.Contracts;
using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;

namespace PSPad.App.State.Investments;

public static class InvestmentLinkRate
{
    public static LinkRate Choose(string currency, DateOnly day, NbpRateView? nbp, RateSuggestion? suggestion)
    {
        if (currency == Currencies.Pln)
        {
            return new LinkRate(1m, day);
        }

        if (nbp is not null && nbp.Currency == currency)
        {
            return new LinkRate(nbp.Rate, nbp.EffectiveDate);
        }

        return suggestion is null ? new LinkRate(null, day) : new LinkRate(suggestion.RateToPln, suggestion.RateDate ?? day);
    }

    public static LinkRate Typed(decimal? rate, DateOnly day) => new(rate, day);
}
