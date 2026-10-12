namespace PSPad.Backup.Scheduling;

public sealed class DailySchedule(TimeOnly at, TimeZoneInfo zone)
{
    public DateTimeOffset NextAfter(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        for (var day = today; ; day = day.AddDays(1))
        {
            var candidate = Occurrence(day);
            if (candidate > now)
            {
                return candidate;
            }
        }
    }

    public string Describe() => $"daily at {at:HH:mm} {zone.Id}";

    DateTimeOffset Occurrence(DateOnly day)
    {
        var local = day.ToDateTime(at);

        // A wall-clock time that spring-forward skips runs at the same distance past the jump.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
