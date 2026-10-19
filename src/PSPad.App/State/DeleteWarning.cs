using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public static class DeleteWarning
{
    public static string ForArea(
        string name, Guid areaId, IEnumerable<TaskList> lists, IEnumerable<TodoTask> tasks, IEnumerable<ReferenceItem> items,
        IStringLocalizer<ListsStrings> localizer)
    {
        var listIds = lists.Where(list => list.AreaId == areaId && !list.Deleted).Select(list => list.Id).ToHashSet();
        var taskCount = tasks.Count(task => listIds.Contains(task.ListId) && !task.Deleted);
        var itemCount = items.Count(item => listIds.Contains(item.ListId) && !item.Deleted);
        return Message(name, [
            Counted(listIds.Count, "ContainsLists", localizer),
            Counted(taskCount, "ContainsTasks", localizer),
            Counted(itemCount, "ContainsItems", localizer)], localizer);
    }

    public static string ForList(string name, Guid listId, IEnumerable<TodoTask> tasks, IStringLocalizer<ListsStrings> localizer) =>
        Message(name, [Counted(tasks.Count(task => task.ListId == listId && !task.Deleted), "ContainsTasks", localizer)], localizer);

    public static string ForList(string name, Guid listId, IEnumerable<ReferenceItem> items, IStringLocalizer<ListsStrings> localizer) =>
        Message(name, [Counted(items.Count(item => item.ListId == listId && !item.Deleted), "ContainsItems", localizer)], localizer);

    static string Message(string name, IEnumerable<string?> contents, IStringLocalizer<ListsStrings> localizer)
    {
        var named = contents.OfType<string>().ToArray();
        return named.Length switch
        {
            0 => localizer["DeleteWarning", name],
            1 => localizer["DeleteWarningWith", name, named[0]],
            _ => localizer["DeleteWarningWith", name, localizer["ContentsAnd", string.Join(", ", named[..^1]), named[^1]].Value]
        };
    }

    static string? Counted(int count, string key, IStringLocalizer<ListsStrings> localizer) =>
        count == 0 ? null : localizer.Plural(count, key);
}
