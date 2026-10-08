using System.Globalization;

namespace PSPad.App.State;

public static class DayQuery
{
    const string Format = "yyyy-MM-dd";

    public static DateOnly From(string uri, DateOnly today) =>
        DateOnly.TryParseExact(QueryString.Value(uri, "day"), Format, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day) ? day : today;

    public static string For(string uri, DateOnly day, DateOnly today) =>
        day == today ? QueryString.Without(uri) : $"{QueryString.Without(uri)}?day={day.ToString(Format, CultureInfo.InvariantCulture)}";

    public static string Keep(string uri) =>
        QueryString.Value(uri, "day") is { } day ? $"{QueryString.Without(uri)}?day={day}" : QueryString.Without(uri);
}
