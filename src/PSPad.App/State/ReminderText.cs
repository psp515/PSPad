using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class ReminderText
{
    public static readonly TimeOnly DefaultAt = new(9, 0);

    static readonly TimeSpan SameDayLead = TimeSpan.FromHours(1);

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

    public static IReadOnlyList<TaskReminder> Presets(TaskTime? time)
    {
        var sameDay = time is null
            ? DefaultAt
            : time.Start.ToTimeSpan() < SameDayLead ? TimeOnly.MinValue : time.Start.Add(-SameDayLead);
        return
        [
            new(0, sameDay), new(1, DefaultAt), new(2, DefaultAt), new(7, DefaultAt)
        ];
    }
}
