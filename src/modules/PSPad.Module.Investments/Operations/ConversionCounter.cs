using PSPad.Module.Money.Values;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Operations;

public static class ConversionCounter
{
    public static MoneyValue Of(MoneyValue money, decimal amount, string currency, DateOnly day)
    {
        var rate = string.Equals(currency, Currencies.Pln, StringComparison.OrdinalIgnoreCase)
            ? 1m
            : Math.Round(money.Amount * money.RateToPln / amount, 6, MidpointRounding.AwayFromZero);
        return new MoneyValue(amount, currency, rate, day);
    }
}
