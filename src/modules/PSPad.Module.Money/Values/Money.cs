using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money;

public sealed record Money(decimal Amount, string Currency, decimal RateToPln, DateOnly RateDate)
{
    public const decimal MaxAmount = 1_000_000_000m;
    public const decimal MaxRate = 10_000m;

    [JsonIgnore]
    public decimal InPln => Math.Round(Amount * RateToPln, 2, MidpointRounding.AwayFromZero);

    public static Money Require(Money requested, DateOnly ownDate)
    {
        var currency = Currencies.Require(requested.Currency);

        if (requested.Amount <= 0)
        {
            throw new DomainRejectedException("An amount has to be more than zero.");
        }

        if (requested.Amount > MaxAmount)
        {
            throw new DomainRejectedException("An amount can be at most 1 000 000 000.");
        }

        if (currency == Currencies.Pln)
        {
            return new Money(requested.Amount, currency, 1m, ownDate);
        }

        if (requested.RateToPln <= 0)
        {
            throw new DomainRejectedException("A rate to PLN has to be more than zero.");
        }

        return requested.RateToPln > MaxRate
            ? throw new DomainRejectedException("A rate can be at most 10 000.")
            : requested with { Currency = currency };
    }
}
