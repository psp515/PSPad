using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class DeleteWarning
{
    public static string ForArea(
        string name, Guid areaId, IEnumerable<TaskList> lists, IEnumerable<TodoTask> tasks, IEnumerable<ReferenceItem> items)
    {
        var listIds = lists.Where(list => list.AreaId == areaId && !list.Deleted).Select(list => list.Id).ToHashSet();
        var taskCount = tasks.Count(task => listIds.Contains(task.ListId) && !task.Deleted);
        var itemCount = items.Count(item => listIds.Contains(item.ListId) && !item.Deleted);
        return Message(name, [Counted(listIds.Count, "list"), Counted(taskCount, "task"), Counted(itemCount, "item")]);
    }

    public static string ForList(string name, Guid listId, IEnumerable<TodoTask> tasks) =>
        Message(name, [Counted(tasks.Count(task => task.ListId == listId && !task.Deleted), "task")]);

    public static string ForList(string name, Guid listId, IEnumerable<ReferenceItem> items) =>
        Message(name, [Counted(items.Count(item => item.ListId == listId && !item.Deleted), "item")]);

    static string Message(string name, IEnumerable<string?> contents)
    {
        var named = contents.OfType<string>().ToArray();
        var what = named.Length switch
        {
            0 => "",
            1 => $" and its {named[0]}",
            _ => $" and its {string.Join(", ", named[..^1])} and {named[^1]}"
        };
        return $"Delete “{name}”{what}? This can’t be undone.";
    }

    static string? Counted(int count, string noun) =>
        count switch
        {
            0 => null,
            1 => $"1 {noun}",
            _ => $"{count} {noun}s"
        };
}
