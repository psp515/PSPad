namespace PSPad.App.State;

public static class TaskQuery
{
    const string NewTask = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(Value(uri, "task"), out var id) ? id : null;

    public static Guid? NewTaskListFrom(string uri) =>
        Value(uri, "task") == NewTask && Guid.TryParse(Value(uri, "list"), out var listId) ? listId : null;

    public static string ForNewTask(string uri, Guid listId) => $"{Without(uri)}?task={NewTask}&list={listId}";

    public static string Without(string uri) => uri.Split('?')[0];

    static string? Value(string uri, string key)
    {
        var mark = uri.IndexOf('?');

        if (mark < 0)
        {
            return null;
        }

        foreach (var pair in uri[(mark + 1)..].Split('&'))
        {
            var parts = pair.Split('=', 2);

            if (parts.Length == 2 && parts[0] == key)
            {
                return parts[1];
            }
        }

        return null;
    }
}
