using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Lists;

public static class TaskListCascade
{
    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Delete(
        TaskList? list, DeleteTaskList command, IReadOnlyList<TodoTask> tasks, DateTimeOffset at)
    {
        var listEvents = TaskList.Decide(list, command, at);
        list!.ApplyAll(listEvents);

        var taskDeletions = tasks
            .Where(task => task.ListId == list.Id && task.UserId == command.UserId && !task.Deleted)
            .Select(task => DeleteTask(task, command, at));

        return [(list, listEvents), .. taskDeletions];
    }

    static (Aggregate, IReadOnlyList<DomainEvent>) DeleteTask(TodoTask task, DeleteTaskList command, DateTimeOffset at)
    {
        var events = TodoTask.Decide(task, new DeleteTask(command.CommandId, command.UserId, task.Id), at);
        task.ApplyAll(events);
        return (task, events);
    }
}
