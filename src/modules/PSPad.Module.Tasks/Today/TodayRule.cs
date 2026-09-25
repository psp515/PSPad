using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Today;

public static class TodayRule
{
    public static DateOnly TodayIn(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).Date);

    public static IReadOnlyList<TodayEntry> Select(IEnumerable<TodoTask> tasks, DateOnly today) =>
        tasks
            .Where(task => !task.Deleted)
            .Select(task => Consider(task, today))
            .OfType<TodayEntry>()
            .OrderByDescending(entry => entry.Overdue)
            .ThenByDescending(entry => entry.Starred)
            .ThenByDescending(entry => entry.Priority)
            .ThenBy(entry => entry.DueOn ?? today)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public const int UpcomingDays = 7;

    public static DayPlan Plan(IEnumerable<TodoTask> tasks, DateOnly today, TimeZoneInfo zone)
    {
        var live = tasks.Where(task => !task.Deleted).ToArray();
        var due = Select(live, today);
        var ahead = live
            .Select(task => Ahead(task, today))
            .OfType<TodayEntry>()
            .OrderBy(entry => entry.DueOn)
            .ThenByDescending(entry => entry.Starred)
            .ThenByDescending(entry => entry.Priority)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var completed = live
            .Where(task => CompletedOn(task, today, zone))
            .OrderByDescending(task => task.CompletedAt)
            .ThenBy(task => task.Name, StringComparer.OrdinalIgnoreCase)
            .Select(task => Entry(task, overdue: false, dueOn: task.IsRecurring ? today : task.DueOn))
            .ToArray();

        return new DayPlan(
            [.. due.Where(entry => entry.Overdue)],
            [.. due.Where(entry => !entry.Overdue)],
            [.. ahead.Where(entry => entry.DueOn == today.AddDays(1))],
            [.. ahead.Where(entry => entry.DueOn > today.AddDays(1))],
            completed);
    }

    public static bool CompletedOn(TodoTask task, DateOnly day, TimeZoneInfo zone) =>
        task.IsRecurring
            ? task.CompletedDays.Contains(day)
            : task.CompletedAt is { } at && TodayIn(at, zone) == day;

    static TodayEntry? Ahead(TodoTask task, DateOnly today)
    {
        var next = task.IsRecurring ? NextOccurrence(task, today) : Upcoming(task, today);

        return next is null ? null : Entry(task, overdue: false, dueOn: next);
    }

    static DateOnly? NextOccurrence(TodoTask task, DateOnly today)
    {
        for (var day = today.AddDays(1); day <= today.AddDays(UpcomingDays); day = day.AddDays(1))
        {
            if (task.Recurrence!.OccursOn(day) && !task.CompletedDays.Contains(day))
            {
                return day;
            }
        }

        return null;
    }

    static DateOnly? Upcoming(TodoTask task, DateOnly today)
    {
        if (task.CompletedAt is not null || task.Starred)
        {
            return null;
        }

        var trigger = EarliestTrigger(task);

        return trigger > today && trigger <= today.AddDays(UpcomingDays) ? trigger : null;
    }

    static TodayEntry? Consider(TodoTask task, DateOnly today)
    {
        if (task.IsRecurring)
        {
            var due = task.Recurrence!.OccursOn(today) && !task.CompletedDays.Contains(today);
            return due ? Entry(task, overdue: false, dueOn: today) : null;
        }

        if (task.CompletedAt is not null)
        {
            return null;
        }

        var trigger = EarliestTrigger(task);
        if (trigger <= today)
        {
            return Entry(task, overdue: trigger < today, dueOn: trigger);
        }

        return task.Starred ? Entry(task, overdue: false, dueOn: trigger) : null;
    }

    static DateOnly? EarliestTrigger(TodoTask task)
    {
        var stepDue = task.NextUncheckedStep?.DueOn;

        return (task.DueOn, stepDue) switch
        {
            (null, null) => null,
            (var due, null) => due,
            (null, var step) => step,
            var (due, step) => due <= step ? due : step
        };
    }

    static TodayEntry Entry(TodoTask task, bool overdue, DateOnly? dueOn) =>
        new(task.Id, task.ListId, task.Name, overdue, dueOn, task.IsRecurring,
            task.Starred, (int)task.Priority);
}
