using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Money.Values;

namespace PSPad.App.Components.Investments;

public static class InvestmentStatTiles
{
    public static IReadOnlyList<StatTile> For(
        AccountFigures figures, IStringLocalizer<InvestmentsStrings> text, SecondaryFigures? secondary = null)
    {
        var currency = figures.Currency;
        var tiles = new List<StatTile>
        {
            Plain("pspad-investment-put-in", text["PutIn"], figures.PutIn, currency,
                MoneySecondary(secondary, pln => pln.PutInPln)),
            Plain("pspad-investment-taken-out", text["TakenOut"], figures.TakenOut, currency,
                MoneySecondary(secondary, pln => pln.TakenOutPln)),
        };
        if (figures.MovedIn != 0)
        {
            tiles.Add(Plain("pspad-investment-moved-in", text["MovedIn"], figures.MovedIn, currency,
                    MoneySecondary(secondary, pln => pln.MovedInPln)));
        }

        if (figures.MovedOut != 0)
        {
            tiles.Add(Plain("pspad-investment-moved-out", text["MovedOut"], figures.MovedOut, currency,
                    MoneySecondary(secondary, pln => pln.MovedOutPln)));
        }

        tiles.AddRange(Results(currency, figures.RealizedGains, figures.RealizedLosses, figures.Unrealized, figures.DividendsNet,
            figures.PassiveIncome, figures.Fees, text, secondary));
        if (figures.AverageRate is { } rate)
        {
            tiles.Add(new StatTile("pspad-investment-average-rate", text["AverageRate"],
                text["AverageRateValue", currency, rate.ToString("N4", CultureInfo.CurrentCulture)], false));
        }

        if (figures.PlnSpent is { } spent)
        {
            tiles.Add(new StatTile("pspad-investment-pln-spent", text["PlnSpent", currency],
                InvestmentText.Money(spent, Currencies.Pln), false));
        }

        return tiles;
    }

    public static IReadOnlyList<StatTile> For(
        PortfolioFigures figures, IStringLocalizer<InvestmentsStrings> text, SecondaryFigures? secondary = null)
    {
        var tiles = new List<StatTile>
        {
            Plain("pspad-investment-put-in", text["PutIn"], figures.PutInPln, Currencies.Pln,
                MoneySecondary(secondary, pln => pln.PutInPln)),
            Plain("pspad-investment-taken-out", text["TakenOut"], figures.TakenOutPln, Currencies.Pln,
                MoneySecondary(secondary, pln => pln.TakenOutPln)),
        };
        tiles.AddRange(Results(Currencies.Pln, figures.RealizedGainsPln, figures.RealizedLossesPln, figures.UnrealizedPln,
            figures.DividendsNetPln, figures.PassiveIncomePln, figures.FeesPln, text, secondary));
        tiles.Add(Signed("pspad-investment-currency", text["CurrencyDifference"], figures.CurrencyDifferencePln, Currencies.Pln,
            SignedSecondary(secondary, pln => pln.CurrencyDifferencePln)));
        return tiles;
    }

    static IEnumerable<StatTile> Results(
        string currency, decimal gains, decimal losses, decimal unrealized, decimal dividendsNet, decimal passive, decimal fees,
        IStringLocalizer<InvestmentsStrings> text, SecondaryFigures? secondary) =>
    [
        Signed("pspad-investment-realized", text["RealizedResult"], gains + losses, currency,
            SignedSecondary(secondary, pln => pln.RealizedResultPln)),
        Signed("pspad-investment-gains", text["RealizedGains"], gains, currency, SignedSecondary(secondary, pln => pln.RealizedGainsPln)),
        Signed("pspad-investment-losses", text["RealizedLosses"], losses, currency,
            SignedSecondary(secondary, pln => pln.RealizedLossesPln)),
        Signed("pspad-investment-unrealized", text["Unrealized"], unrealized, currency,
            SignedSecondary(secondary, pln => pln.UnrealizedPln)),
        Signed("pspad-investment-dividends", text["DividendsNet"], dividendsNet, currency,
            SignedSecondary(secondary, pln => pln.DividendsNetPln)),
        Signed("pspad-investment-passive", text["PassiveIncome"], passive, currency,
            SignedSecondary(secondary, pln => pln.PassiveIncomePln)),
        Signed("pspad-investment-fees", text["Fees"], -fees, currency, SignedSecondary(secondary, pln => -pln.FeesPln)),
    ];

    static string? SignedSecondary(SecondaryFigures? secondary, Func<PortfolioFigures, decimal> pick) =>
        secondary is null ? null : secondary.Signed(pick(secondary.Pln));

    static string? MoneySecondary(SecondaryFigures? secondary, Func<PortfolioFigures, decimal> pick) =>
        secondary is null ? null : secondary.Money(pick(secondary.Pln));

    static StatTile Plain(string css, string label, decimal amount, string currency, string? secondary) =>
        new(css, label, InvestmentText.Money(amount, currency), false, secondary);

    static StatTile Signed(string css, string label, decimal amount, string currency, string? secondary) =>
        new(css, label, InvestmentText.Signed(amount, currency), amount < 0, secondary);
}
