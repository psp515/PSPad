using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public static class LocalToday
{
    public static DateOnly In(DateTimeOffset utcNow, string timeZone) =>
        TodayRule.TodayIn(utcNow, Zone(timeZone));

    public static TimeZoneInfo Zone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
