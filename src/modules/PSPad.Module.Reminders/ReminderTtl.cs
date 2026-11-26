using PSPad.Module.Tasks.Today;

namespace PSPad.Module.Reminders;

public static class ReminderTtl
{
    public static TimeSpan For(DateOnly occurrence, TimeZoneInfo zone, DateTimeOffset now)
    {
        var end = ReminderRule.MomentOf(occurrence.AddDays(1), TimeOnly.MinValue, zone);
        var left = end - now;
        return TimeSpan.FromSeconds(Math.Clamp(left.TotalSeconds, 60, 86400));
    }
}
