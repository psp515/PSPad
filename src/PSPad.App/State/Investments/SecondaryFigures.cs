using PSPad.Module.Investments.Reading;
using PSPad.Module.Money.Values;

namespace PSPad.App.State.Investments;

public sealed record SecondaryFigures(PortfolioFigures Pln, string Currency, decimal CurrencyRate, decimal AccountRate)
{
    const string Prefix = "≈ ";

    public static SecondaryFigures? For(InvestmentOverview overview, Guid? accountId)
    {
        var shown = accountId is { } id ? overview.Account(id)?.Currency : Currencies.Pln;
        if (shown is null || string.Equals(shown, overview.DisplayCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var pln = accountId is { } chosen ? overview.Portfolio.Accounts.GetValueOrDefault(chosen) : overview.Portfolio;
        return pln is not null
               && overview.Fx.ToPln(overview.DisplayCurrency, overview.Today) is { } currencyRate && currencyRate > 0
               && overview.Fx.ToPln(shown, overview.Today) is { } accountRate
            ? new SecondaryFigures(pln, overview.DisplayCurrency, currencyRate, accountRate)
            : null;
    }

    public string Money(decimal pln) => Prefix + InvestmentText.Money(In(pln), Currency);

    public string Signed(decimal pln) => Prefix + InvestmentText.Signed(In(pln), Currency);

    public string MoneyFromAccount(decimal amount) => Money(amount * AccountRate);

    public string SignedFromAccount(decimal amount) => Signed(amount * AccountRate);

    decimal In(decimal pln) => Math.Round(pln / CurrencyRate, 2, MidpointRounding.AwayFromZero);
}
