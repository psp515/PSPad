using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money;

public sealed record Money(decimal Amount, string Currency, decimal RateToPln, DateOnly RateDate)
{
    [JsonIgnore]
    public decimal InPln => Math.Round(Amount * RateToPln, 2, MidpointRounding.AwayFromZero);

    public static Money Require(Money requested, DateOnly ownDate)
    {
        var currency = Currencies.Require(requested.Currency);

        if (requested.Amount <= 0)
        {
            throw new DomainRejectedException("An amount has to be more than zero.");
        }

        if (currency == Currencies.Pln)
        {
            return new Money(requested.Amount, currency, 1m, ownDate);
        }

        return requested.RateToPln <= 0
            ? throw new DomainRejectedException("A rate to PLN has to be more than zero.")
            : requested with { Currency = currency };
    }
}
