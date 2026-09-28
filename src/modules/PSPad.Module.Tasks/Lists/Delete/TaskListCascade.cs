using PSPad.Abstractions;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Lists;

public static class TaskListCascade
{
    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Delete(
        TaskList? list, DeleteTaskList command, IReadOnlyList<TodoTask> tasks, IReadOnlyList<ReferenceItem> items,
        DateTimeOffset at)
    {
        var listEvents = TaskList.Decide(list, command, at);
        list!.ApplyAll(listEvents);

        var taskDeletions = tasks
            .Where(task => task.ListId == list.Id && task.UserId == command.UserId && !task.Deleted)
            .Select(task => DeleteTask(task, command, at));

        var itemDeletions = items
            .Where(item => item.ListId == list.Id && item.UserId == command.UserId && !item.Deleted)
            .Select(item => DeleteItem(item, command, at));

        return [(list, listEvents), .. taskDeletions, .. itemDeletions];
    }

    static (Aggregate, IReadOnlyList<DomainEvent>) DeleteTask(TodoTask task, DeleteTaskList command, DateTimeOffset at)
    {
        var events = TodoTask.Decide(task, new DeleteTask(command.CommandId, command.UserId, task.Id), at);
        task.ApplyAll(events);
        return (task, events);
    }

    static (Aggregate, IReadOnlyList<DomainEvent>) DeleteItem(ReferenceItem item, DeleteTaskList command, DateTimeOffset at)
    {
        var events = ReferenceItem.Decide(item, new DeleteReferenceItem(command.CommandId, command.UserId, item.Id), at);
        item.ApplyAll(events);
        return (item, events);
    }
}
