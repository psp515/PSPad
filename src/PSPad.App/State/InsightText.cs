using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.Module.Money.Reading;

namespace PSPad.App.State;

public static class InsightText
{
    public static string Title(Insight insight, IStringLocalizer<BudgetsStrings> text) => insight.Kind switch
    {
        InsightKind.PastUsual => text["InsightPastUsual", insight.Category],
        InsightKind.RunningHigh => text["InsightRunningHigh", insight.Category],
        InsightKind.IncomeExpected => text["InsightIncomeExpected", insight.Category, Ordinal(insight.Day, text)],
        _ => text["InsightOnTrack", insight.Category]
    };

    public static string Caption(Insight insight, IStringLocalizer<BudgetsStrings> text) => insight.Kind switch
    {
        InsightKind.PastUsual => text["InsightPastUsualCaption", MoneyText.Pln(insight.Amount), MoneyText.Pln(insight.Usual)],
        InsightKind.RunningHigh => text["InsightRunningHighCaption",
            MoneyText.Pln(insight.Amount), MoneyText.Pln(insight.Amount - insight.Usual)],
        InsightKind.IncomeExpected => text["InsightIncomeExpectedCaption", MoneyText.Pln(insight.Amount)],
        _ => text["InsightOnTrackCaption", MoneyText.Pln(insight.Amount)]
    };

    static string Ordinal(int day, IStringLocalizer<BudgetsStrings> text)
    {
        var key = day % 100 is 11 or 12 or 13 ? "OrdinalTh" : (day % 10) switch
        {
            1 => "OrdinalSt",
            2 => "OrdinalNd",
            3 => "OrdinalRd",
            _ => "OrdinalTh"
        };
        return text[key, day];
    }
}
