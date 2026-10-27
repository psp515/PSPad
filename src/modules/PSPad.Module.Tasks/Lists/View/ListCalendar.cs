using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Module.Tasks.Lists;

public static class ListCalendar
{
    public static IReadOnlyList<CalendarDay> Month(IEnumerable<TodoTask> tasks, int year, int month, DateOnly today)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        return [.. tasks
            .Where(task => !task.Deleted)
            .SelectMany(task => Placements(task, first, last, today))
            .GroupBy(placement => placement.Day)
            .OrderBy(group => group.Key)
            .Select(group => new CalendarDay(group.Key, [.. group.Select(placement => placement.Entry)]))];
    }

    static IEnumerable<(DateOnly Day, CalendarEntry Entry)> Placements(TodoTask task, DateOnly first, DateOnly last, DateOnly today)
    {
        if (task.IsRecurring)
        {
            for (var day = first < today ? today : first; day <= last; day = day.AddDays(1))
            {
                if (task.OccursOn(day) && !task.CompletedDays.Contains(day))
                {
                    yield return (day, new CalendarEntry(task.Id, false, true));
                }
            }

            yield break;
        }

        if (task.CompletedAt is null && TodayRule.TriggerOf(task) is { } trigger && trigger >= first && trigger <= last)
        {
            yield return (trigger, new CalendarEntry(task.Id, trigger < today, false));
        }
    }
}
