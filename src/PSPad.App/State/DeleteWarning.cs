using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class DeleteWarning
{
    public static string ForArea(string name, Guid areaId, IEnumerable<TaskList> lists, IEnumerable<TodoTask> tasks)
    {
        var listIds = lists.Where(list => list.AreaId == areaId && !list.Deleted).Select(list => list.Id).ToHashSet();
        var taskCount = tasks.Count(task => listIds.Contains(task.ListId) && !task.Deleted);
        return Message(name, [Counted(listIds.Count, "list"), Counted(taskCount, "task")]);
    }

    public static string ForList(string name, Guid listId, IEnumerable<TodoTask> tasks) =>
        Message(name, [Counted(tasks.Count(task => task.ListId == listId && !task.Deleted), "task")]);

    static string Message(string name, IEnumerable<string?> contents)
    {
        var named = contents.OfType<string>().ToArray();
        var what = named.Length == 0 ? "" : $" and its {string.Join(" and ", named)}";
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
