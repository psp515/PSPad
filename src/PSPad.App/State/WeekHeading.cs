using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;

namespace PSPad.App.State;

public static class WeekHeading
{
    public static string Label(DateOnly day, DateOnly today, IStringLocalizer<SharedStrings> shared) =>
        Math.Abs(day.DayNumber - today.DayNumber) <= 1
            ? $"{DueDayText.Describe(day, today, shared)} · {day.ToString("ddd, d MMM")}"
            : day.ToString(day.Year == today.Year ? "ddd, d MMM" : "ddd, d MMM yyyy");

    public static string Meta(DateOnly day, IStringLocalizer<TodayStrings> text) =>
        text["WeekMeta", ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue)), day.ToString("MMMM yyyy")];
}
