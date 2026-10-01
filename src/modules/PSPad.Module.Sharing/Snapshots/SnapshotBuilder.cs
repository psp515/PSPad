using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Sharing.Snapshots;

public static class SnapshotBuilder
{
    public static ListSnapshot Build(
        Guid id, string token, TaskList list, IEnumerable<TodoTask> tasks,
        IEnumerable<ReferenceItem> items, DateTimeOffset now, DateTimeOffset expiresAt, DateOnly today) =>
        new()
        {
            Id = id,
            Token = token,
            UserId = list.UserId,
            ListId = list.Id,
            Kind = list.Kind,
            Name = list.Name,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            Tasks = OrderTasks(tasks, list.Id, today)
                .Select(task => ToSnapshotTask(task, id, today))
                .ToArray(),
            Items = OrderItems(items, list.Id)
                .Select(item => ToSnapshotItem(item, id))
                .ToArray()
        };

    static IEnumerable<TodoTask> OrderTasks(IEnumerable<TodoTask> tasks, Guid listId, DateOnly today) =>
        tasks
            .Where(task => !task.Deleted && task.ListId == listId)
            .OrderBy(task => IsDone(task, today))
            .ThenByDescending(task => task.Starred)
            .ThenBy(task => task.DueOn ?? DateOnly.MaxValue)
            .ThenBy(task => task.CreatedAt ?? DateTimeOffset.MinValue)
            .ThenBy(task => task.Id);

    static IEnumerable<ReferenceItem> OrderItems(IEnumerable<ReferenceItem> items, Guid listId) =>
        items
            .Where(item => !item.Deleted && item.ListId == listId)
            .OrderByDescending(item => item.Starred)
            .ThenBy(item => item.Position);

    static bool IsDone(TodoTask task, DateOnly today) => task.CompletedAt is not null || task.EndedBy(today);

    static SnapshotTask ToSnapshotTask(TodoTask task, Guid snapshotId, DateOnly today)
    {
        var mark = task.SnapshotMarks.FirstOrDefault(entry => entry.SnapshotId == snapshotId && entry.StepId is null);

        return new SnapshotTask(
            task.Id,
            task.Name,
            IsDone(task, today),
            task.DueOn,
            task.Priority,
            task.Starred,
            task.Description,
            mark is not null,
            mark?.MarkedAt,
            task.Steps.Select(step => ToSnapshotStep(step, task, snapshotId)).ToArray());
    }

    static SnapshotStep ToSnapshotStep(Step step, TodoTask task, Guid snapshotId)
    {
        var mark = task.SnapshotMarks.FirstOrDefault(entry => entry.SnapshotId == snapshotId && entry.StepId == step.Id);

        return new SnapshotStep(step.Id, step.Name, step.Checked, mark is not null, mark?.MarkedAt);
    }

    static SnapshotItem ToSnapshotItem(ReferenceItem item, Guid snapshotId)
    {
        var mark = item.SnapshotMarks.FirstOrDefault(entry => entry.SnapshotId == snapshotId);

        return new SnapshotItem(
            item.Id,
            item.Name,
            item.Description,
            item.Starred,
            mark is not null,
            mark?.MarkedAt,
            item.Fields.Select(field => new SnapshotField(field.Label, field.Value, field.Display)).ToArray());
    }
}
