using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class ReminderText
{
    public static readonly TimeOnly DefaultAt = new(9, 0);

    public static readonly IReadOnlyList<int> QuickDays = [0, 1, 2, 7];

    public static string Describe(TaskReminder? reminder, IStringLocalizer<TasksStrings> localizer)
    {
        if (reminder is null)
        {
            return localizer["ReminderOff"];
        }

        var at = TaskTimeText.Format(reminder.At);
        return reminder.DaysBefore == 0
            ? string.Format(localizer["ReminderOnTheDay"].Value, at)
            : localizer.Plural(reminder.DaysBefore, "ReminderDaysBefore", at);
    }

    public static string Quick(int days, IStringLocalizer<TasksStrings> localizer) => days switch
    {
        0 => localizer["ReminderQuickSameDay"],
        7 => localizer["ReminderQuickWeek"],
        _ => localizer.Plural(days, "ReminderQuickDays")
    };

    public static TaskReminder WithDays(TaskReminder? current, int days, TaskTime? time) =>
        new(days, current?.At ?? time?.Start ?? DefaultAt);

    public static TaskReminder WithAt(TaskReminder? current, TimeOnly at) =>
        new(current?.DaysBefore ?? 1, at);

    public static bool TryParseDays(string? text, out int days) =>
        int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out days)
        && days <= TaskReminder.MostDaysBefore;
}
