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
        if (trigger is null || trigger > today)
        {
            return null;
        }

        return Entry(task, overdue: trigger < today, dueOn: trigger);
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
