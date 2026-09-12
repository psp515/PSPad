using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Recurrence;

public static class Occurrences
{
    public static IReadOnlyList<Occurrence> Between(TodoTask task, DateOnly from, DateOnly to, DateOnly today)
    {
        if (task.Recurrence is null)
        {
            return [];
        }

        var occurrences = new List<Occurrence>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (task.Recurrence.OccursOn(day))
            {
                occurrences.Add(new Occurrence(day, StatusOf(task, day, today)));
            }
        }

        return occurrences;
    }

    static OccurrenceStatus StatusOf(TodoTask task, DateOnly day, DateOnly today)
    {
        if (task.CompletedDays.Contains(day))
        {
            return OccurrenceStatus.Done;
        }

        return day < today ? OccurrenceStatus.Skipped : OccurrenceStatus.Pending;
    }
}
