using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Analytics;

public static class BurndownRule
{
    public static BurndownSeries Build(
        IEnumerable<TodoTask> tasks, DateOnly today, int days, TimeZoneInfo zone)
    {
        var material = tasks.Where(task => !task.Deleted).ToArray();
        var points = new List<BurndownPoint>(days);

        for (var offset = days - 1; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);

            points.Add(new BurndownPoint(
                day,
                material.Count(task => IsOpenOn(task, day, zone)),
                material.Sum(task => CompletionsOn(task, day, zone))));
        }

        return new BurndownSeries(points);
    }

    static bool IsOpenOn(TodoTask task, DateOnly day, TimeZoneInfo zone)
    {
        if (task.IsRecurring)
        {
            return false;
        }

        if (task.CreatedAt is not null && DayOf(task.CreatedAt.Value, zone) > day)
        {
            return false;
        }

        return task.CompletedAt is null || DayOf(task.CompletedAt.Value, zone) > day;
    }

    static int CompletionsOn(TodoTask task, DateOnly day, TimeZoneInfo zone)
    {
        if (task.IsRecurring)
        {
            return task.CompletedDays.Contains(day) ? 1 : 0;
        }

        return task.CompletedAt is not null && DayOf(task.CompletedAt.Value, zone) == day ? 1 : 0;
    }

    static DateOnly DayOf(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
}
