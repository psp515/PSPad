using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.Components.Investments;

public sealed record ClosedPositionText(string Sub, decimal? Share)
{
    public static ClosedPositionText For(ClosedPosition closed, TimeZoneInfo zone, IStringLocalizer<InvestmentsStrings> text)
    {
        var bought = closed.FirstBought is { } first ? DayText(first, zone) : "";
        var dates = $"{bought} → {DayText(closed.LastSold, zone)}".TrimStart();
        return new ClosedPositionText($"{text["SoldQuantity", InvestmentText.Quantity(closed.Quantity)].Value} · {dates}",
            closed.ResultShare);
    }

    public static string DayText(DateTimeOffset at, TimeZoneInfo zone) =>
        InvestmentText.Day(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime));
}
