using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class GoalProgress
{
    public static IReadOnlyList<GoalProgressWeek> Of(IEnumerable<TodoTask> tasks, string timeZone, DateOnly today)
    {
        var zone = LocalToday.Zone(timeZone);
        var counted = tasks
            .Where(task => !task.Deleted && !task.IsRecurring && task.CreatedAt is not null)
            .Select(task => (Created: DayIn(task.CreatedAt!.Value, zone), Done: task.CompletedAt is { } done ? DayIn(done, zone) : (DateOnly?)null))
            .ToArray();

        if (counted.Length == 0)
        {
            return [];
        }

        var first = MondayOf(counted.Min(task => task.Created));
        var last = MondayOf(today);
        var weeks = new List<GoalProgressWeek>();

        for (var start = first; start <= last; start = start.AddDays(7))
        {
            var end = start.AddDays(6);
            weeks.Add(new GoalProgressWeek(
                start,
                counted.Count(task => task.Created <= end),
                counted.Count(task => task.Done <= end)));
        }

        return weeks;
    }

    static DateOnly DayIn(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
