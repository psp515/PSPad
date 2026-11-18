using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Today;

public static class ReminderRule
{
    public static bool IsDormant(TodoTask task) =>
        task.Reminder is not null && !task.IsRecurring && task.DueOn is null;

    public static IReadOnlyList<ReminderMoment> MomentsBetween(
        TodoTask task, TimeZoneInfo zone, DateTimeOffset from, DateTimeOffset to)
    {
        if (task.Deleted || task.Reminder is not { } reminder || task.ReminderSetAt is not { } setAt || IsDormant(task))
        {
            return [];
        }

        var firstDay = TodayRule.TodayIn(from, zone).AddDays(reminder.DaysBefore - 1);
        var lastDay = TodayRule.TodayIn(to, zone).AddDays(reminder.DaysBefore + 1);
        var moments = new List<ReminderMoment>();

        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            if (!IsPending(task, day))
            {
                continue;
            }

            var moment = MomentOf(day.AddDays(-reminder.DaysBefore), reminder.At, zone);

            if (moment > from && moment <= to && moment >= setAt)
            {
                moments.Add(new ReminderMoment(day, moment));
            }
        }

        return moments;
    }

    public static DateTimeOffset MomentOf(DateOnly day, TimeOnly at, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(at, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    static bool IsPending(TodoTask task, DateOnly day) =>
        task.IsRecurring
            ? task.OccursOn(day) && !task.CompletedDays.Contains(day)
            : task.CompletedAt is null && task.DueOn == day;
}
