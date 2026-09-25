namespace PSPad.App.State;

public static class TaskQuery
{
    const string NewTask = "new";

    public static Guid? From(string uri) =>
        Guid.TryParse(QueryString.Value(uri, "task"), out var id) ? id : null;

    public static Guid? NewTaskListFrom(string uri) =>
        QueryString.Value(uri, "task") == NewTask && Guid.TryParse(QueryString.Value(uri, "list"), out var listId)
            ? listId
            : null;

    public static string ForNewTask(string uri, Guid listId) => $"{Without(uri)}?task={NewTask}&list={listId}";

    public static string Without(string uri) => QueryString.Without(uri);
}
