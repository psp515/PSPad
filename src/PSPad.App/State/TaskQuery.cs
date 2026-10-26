using System.Globalization;

namespace PSPad.App.State;

public static class TaskQuery
{
    const string NewTask = "new";
    const string DueFormat = "yyyy-MM-dd";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "task"), out var id) ? id : null;

    public static bool IsNewTask(string uri) => QueryString.Value(uri, "task") == NewTask;

    public static Guid? NewTaskListFrom(string uri) =>
        IsNewTask(uri) && Guid.TryParse(QueryString.Value(uri, "list"), out var listId)
            ? listId
            : null;

    public static DateOnly? NewTaskDueFrom(string uri) =>
        IsNewTask(uri) && DateOnly.TryParseExact(QueryString.Value(uri, "due"), DueFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var due)
            ? due
            : null;

    public static string ForNewTask(string uri, Guid? listId = null, DateOnly? due = null)
    {
        var kept = DayQuery.Keep(uri);
        var query = $"task={NewTask}"
            + (listId is { } list ? $"&list={list}" : "")
            + (due is { } day ? $"&due={day.ToString(DueFormat, CultureInfo.InvariantCulture)}" : "");
        return $"{kept}{(kept.Contains('?') ? '&' : '?')}{query}";
    }

    public static string For(string uri, Guid taskId)
    {
        var kept = DayQuery.Keep(uri);
        return $"{kept}{(kept.Contains('?') ? '&' : '?')}task={taskId}";
    }

    public static string Without(string uri) => DayQuery.Keep(uri);
}
