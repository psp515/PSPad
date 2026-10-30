namespace PSPad.App.State;

public static class SnapshotExpiry
{
    public static DateTimeOffset EndOfLocalDay(DateOnly day, TimeZoneInfo zone)
    {
        var nextLocalMidnight = DateTime.SpecifyKind(day.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, zone), TimeSpan.Zero);
    }

    public static int DaysLeft(DateTimeOffset expiresAt, DateTimeOffset now) =>
        Math.Max(1, (int)Math.Ceiling((expiresAt - now).TotalDays));
}
