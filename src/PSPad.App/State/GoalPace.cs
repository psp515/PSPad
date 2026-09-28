namespace PSPad.App.State;

public static class GoalPace
{
    public static GoalPaceView? Of(IReadOnlyList<GoalProgressWeek> weeks, DateOnly dueOn, DateOnly today)
    {
        if (weeks.Count == 0 || weeks[^1].Total == 0)
        {
            return null;
        }

        var start = weeks[0].WeekStart;
        var span = dueOn.DayNumber - start.DayNumber;
        var used = today.DayNumber - start.DayNumber;
        var time = span <= 0 ? 100 : Math.Clamp(used * 100 / span, 0, 100);
        var work = weeks[^1].Done * 100 / weeks[^1].Total;

        return new GoalPaceView(time, work, dueOn.DayNumber - today.DayNumber);
    }
}
