using System.Globalization;
using System.Text;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public sealed record ChartScale(DateOnly First, DateOnly Last, decimal Low, decimal High, double Width, double Height, double Padding)
{
    public static ChartScale For(IReadOnlyList<DailyClose> closes, double width, double height, double padding) =>
        closes.Count == 0
            ? new ChartScale(default, default, 0m, 0m, width, height, padding)
            : new ChartScale(closes[0].Date, closes[^1].Date, closes.Min(close => close.Close), closes.Max(close => close.Close),
                width, height, padding);

    public double Left => Padding;

    public double Right => Width - Padding;

    public double Top => Padding;

    public double Bottom => Height - Padding;

    public double X(DateOnly day)
    {
        var span = Last.DayNumber - First.DayNumber;
        return span <= 0 ? Left : Left + (Right - Left) * (day.DayNumber - First.DayNumber) / span;
    }

    public double Y(decimal price)
    {
        var span = High - Low;
        return span <= 0 ? (Top + Bottom) / 2 : Bottom - (Bottom - Top) * (double)((price - Low) / span);
    }

    public string Line(IEnumerable<DailyClose> closes)
    {
        var path = new StringBuilder();
        foreach (var close in closes)
        {
            path.Append(path.Length == 0 ? 'M' : 'L')
                .Append(Number(X(close.Date))).Append(',').Append(Number(Y(close.Close))).Append(' ');
        }

        return path.ToString().TrimEnd();
    }

    public string Area(IReadOnlyList<DailyClose> closes) =>
        closes.Count == 0
            ? ""
            : $"{Line(closes)} L{Number(X(closes[^1].Date))},{Number(Bottom)} L{Number(X(closes[0].Date))},{Number(Bottom)} Z";

    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
