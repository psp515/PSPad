namespace PSPad.App.State;

public static class ReminderZone
{
    static readonly TimeSpan HalfYear = TimeSpan.FromDays(182);

    public static string? Notice(string stored, string browser, DateTimeOffset now)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(browser, out var here)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(stored, out var saved))
        {
            return null;
        }

        var sameClock = saved.GetUtcOffset(now) == here.GetUtcOffset(now)
            && saved.GetUtcOffset(now + HalfYear) == here.GetUtcOffset(now + HalfYear);
        return sameClock ? null : stored;
    }
}
