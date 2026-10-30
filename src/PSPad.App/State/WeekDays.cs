using System.Globalization;

namespace PSPad.App.State;

public static class WeekDays
{
    public const double SwipeThreshold = 48;

    public static DateOnly StartOfWeek(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7));

    public static IEnumerable<DateOnly> WeekOf(DateOnly day) =>
        Enumerable.Range(0, 7).Select(offset => StartOfWeek(day).AddDays(offset));

    public static int SwipeDirection(double dx, double dy) =>
        Math.Abs(dx) >= SwipeThreshold && Math.Abs(dx) > Math.Abs(dy) ? (dx < 0 ? 1 : -1) : 0;
}
