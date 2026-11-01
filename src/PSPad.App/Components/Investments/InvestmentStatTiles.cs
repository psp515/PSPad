using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Money.Values;

namespace PSPad.App.Components.Investments;

public static class InvestmentStatTiles
{
    public static IReadOnlyList<StatTile> For(AccountFigures figures, IStringLocalizer<InvestmentsStrings> text)
    {
        var tiles = Common(figures.Currency, figures.PutIn, figures.TakenOut, figures.RealizedGains, figures.RealizedLosses,
            figures.Unrealized, figures.DividendsNet, figures.PassiveIncome, figures.Fees, text);
        if (figures.AverageRate is { } rate)
        {
            tiles.Add(new StatTile("pspad-investment-average-rate", text["AverageRate"],
                text["AverageRateValue", figures.Currency, rate.ToString("N4", CultureInfo.CurrentCulture)], false));
        }

        if (figures.PlnSpent is { } spent)
        {
            tiles.Add(new StatTile("pspad-investment-pln-spent", text["PlnSpent", figures.Currency],
                InvestmentText.Money(spent, Currencies.Pln), false));
        }

        return tiles;
    }

    public static IReadOnlyList<StatTile> For(PortfolioFigures figures, IStringLocalizer<InvestmentsStrings> text)
    {
        var tiles = Common(Currencies.Pln, figures.PutInPln, figures.TakenOutPln, figures.RealizedGainsPln, figures.RealizedLossesPln,
            figures.UnrealizedPln, figures.DividendsNetPln, figures.PassiveIncomePln, figures.FeesPln, text);
        tiles.Add(Signed("pspad-investment-currency", text["CurrencyDifference"], figures.CurrencyDifferencePln, Currencies.Pln));
        return tiles;
    }

    static List<StatTile> Common(
        string currency, decimal putIn, decimal takenOut, decimal gains, decimal losses, decimal unrealized, decimal dividendsNet,
        decimal passive, decimal fees, IStringLocalizer<InvestmentsStrings> text) =>
    [
        new("pspad-investment-put-in", text["PutIn"], InvestmentText.Money(putIn, currency), false),
        new("pspad-investment-taken-out", text["TakenOut"], InvestmentText.Money(takenOut, currency), false),
        Signed("pspad-investment-realized", text["RealizedResult"], gains + losses, currency),
        Signed("pspad-investment-gains", text["RealizedGains"], gains, currency),
        Signed("pspad-investment-losses", text["RealizedLosses"], losses, currency),
        Signed("pspad-investment-unrealized", text["Unrealized"], unrealized, currency),
        Signed("pspad-investment-dividends", text["DividendsNet"], dividendsNet, currency),
        Signed("pspad-investment-passive", text["PassiveIncome"], passive, currency),
        Signed("pspad-investment-fees", text["Fees"], -fees, currency),
    ];

    static StatTile Signed(string css, string label, decimal amount, string currency) =>
        new(css, label, InvestmentText.Signed(amount, currency), amount < 0);
}
