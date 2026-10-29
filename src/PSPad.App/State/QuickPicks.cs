using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;

namespace PSPad.App.State;

public static class QuickPicks
{
    public static IReadOnlyList<QuickPick> For(
        DateOnly today, IStringLocalizer<SharedStrings> shared, IStringLocalizer<TasksStrings> tasks) =>
    [
        new(shared["Today"], today),
        new(shared["Tomorrow"], today.AddDays(1)),
        new(tasks["QuickInTwoDays"], today.AddDays(2)),
        new(tasks["QuickNextWeek"], NextWeekStart(today))
    ];

    static DateOnly NextWeekStart(DateOnly today)
    {
        var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var daysAhead = ((int)firstDay - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(daysAhead == 0 ? 7 : daysAhead);
    }
}
