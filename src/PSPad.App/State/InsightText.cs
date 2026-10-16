using System.Globalization;
using PSPad.Module.Money.Reading;

namespace PSPad.App.State;

public static class InsightText
{
    public static string Title(Insight insight) => insight.Kind switch
    {
        InsightKind.PastUsual => $"{insight.Category} already past a usual month",
        InsightKind.RunningHigh => $"{insight.Category} is running high",
        InsightKind.IncomeExpected => $"{insight.Category} usually lands around the {Ordinal(insight.Day)}",
        _ => $"{insight.Category} on track"
    };

    public static string Caption(Insight insight) => insight.Kind switch
    {
        InsightKind.PastUsual => $"{MoneyText.Pln(insight.Amount)} vs {MoneyText.Pln(insight.Usual)} average",
        InsightKind.RunningHigh =>
            $"{MoneyText.Pln(insight.Amount)} so far, {MoneyText.Pln(insight.Amount - insight.Usual)} more than usual at this point",
        InsightKind.IncomeExpected => $"{MoneyText.Pln(insight.Amount)} on average; nothing yet this month",
        _ => $"{MoneyText.Pln(insight.Amount)}, close to usual"
    };

    static string Ordinal(int day)
    {
        var suffix = day % 100 is 11 or 12 or 13 ? "th" : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{day.ToString(CultureInfo.InvariantCulture)}{suffix}";
    }
}
