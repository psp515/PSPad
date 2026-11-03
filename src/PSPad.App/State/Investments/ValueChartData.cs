using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.Statistics;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public sealed record ValueChartData(IReadOnlyList<(string Key, double[] Data)> Series, int YStep)
{
    const string Minus = "−";

    public static ValueChartData For(IReadOnlyList<ValuePoint> points, ValueChartMode mode)
    {
        IReadOnlyList<(string Key, double[] Data)> series = mode == ValueChartMode.Profit
            ? [("Profit", [.. points.Select(point => (double)(point.Value - point.NetDeposits))])]
            :
            [
                ("Value", [.. points.Select(point => (double)point.Value)]),
                ("NetDeposits", [.. points.Select(point => (double)point.NetDeposits)])
            ];
        var largest = series.SelectMany(line => line.Data).Select(Math.Abs).DefaultIfEmpty(0d).Max();
        return new ValueChartData(series, ChartAxis.Step(largest));
    }

    public static string Compact(double value, IStringLocalizer<InvestmentsStrings> text)
    {
        var size = Math.Abs(value);
        var label = size >= 1_000_000 ? text["AxisMillions", Short(size / 1_000_000)].Value
            : size >= 1_000 ? text["AxisThousands", Short(size / 1_000)].Value
            : Short(size);
        return value < 0 ? Minus + label : label;
    }

    static string Short(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.CurrentCulture);
}
