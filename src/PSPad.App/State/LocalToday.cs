using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public static class LocalToday
{
    public static DateOnly In(DateTimeOffset utcNow, string timeZone)
    {
        try
        {
            return TodayRule.TodayIn(utcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            Console.Error.WriteLine($"Unknown time zone '{timeZone}': {exception.Message}");

            return TodayRule.TodayIn(utcNow, TimeZoneInfo.Utc);
        }
    }
}
