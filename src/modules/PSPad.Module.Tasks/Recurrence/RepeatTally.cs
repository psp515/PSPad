using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Recurrence;

public sealed record RepeatTally(int Total, int Streak)
{
    public static RepeatTally Of(TodoTask task, DateOnly today)
    {
        if (task.Recurrence is null)
        {
            return new RepeatTally(0, 0);
        }

        var streak = 0;
        var start = task.Recurrence.StartsOn;
        var from = task.CompletedDays.Contains(today) ? today : today.AddDays(-1);
        for (var day = from; day >= start; day = day.AddDays(-1))
        {
            if (!task.OccursOn(day))
            {
                continue;
            }

            if (!task.CompletedDays.Contains(day))
            {
                break;
            }

            streak++;
        }

        return new RepeatTally(task.CompletedDays.Count, streak);
    }
}
