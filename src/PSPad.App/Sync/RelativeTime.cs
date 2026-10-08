namespace PSPad.App.Sync;

public static class RelativeTime
{
    public static string Describe(DateTimeOffset then, DateTimeOffset now)
    {
        var elapsed = now - then;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return Plural((int)elapsed.TotalHours, "hour");
        }

        return Plural((int)elapsed.TotalDays, "day");
    }

    static string Plural(int count, string unit) => count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";
}
