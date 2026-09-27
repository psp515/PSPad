using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class CreatedOn
{
    public static DateOnly? Of(TodoTask task, string timeZone) =>
        task.CreatedAt is { } at
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, LocalToday.Zone(timeZone)).DateTime)
            : null;
}
