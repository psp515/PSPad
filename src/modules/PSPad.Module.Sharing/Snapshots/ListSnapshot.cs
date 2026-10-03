using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Sharing.Snapshots;

public sealed record ListSnapshot
{
    public Guid Id { get; init; }
    public string Token { get; init; } = "";
    public Guid UserId { get; init; }
    public Guid ListId { get; init; }
    public ListKind Kind { get; init; }
    public string Name { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public IReadOnlyList<SnapshotTask> Tasks { get; init; } = [];
    public IReadOnlyList<SnapshotItem> Items { get; init; } = [];

    public bool IsLiveAt(DateTimeOffset now) => ExpiresAt > now;

    public bool Holds(Guid entryId, Guid? stepId) =>
        stepId is { } id
            ? Tasks.Any(task => task.Id == entryId && task.Steps.Any(step => step.Id == id))
            : Tasks.Any(task => task.Id == entryId) || Items.Any(item => item.Id == entryId);

    public ListSnapshot WithMark(Guid entryId, Guid? stepId, bool marked, DateTimeOffset at)
    {
        if (stepId is { } id)
        {
            return WithStepMark(entryId, id, marked, at);
        }

        var taskIndex = Tasks.ToList().FindIndex(task => task.Id == entryId);
        if (taskIndex >= 0)
        {
            var task = Tasks[taskIndex];
            if (task.Marked == marked)
            {
                return this;
            }

            var tasks = Tasks.ToArray();
            tasks[taskIndex] = task with { Marked = marked, MarkedAt = marked ? at : null };
            return this with { Tasks = tasks };
        }

        var itemIndex = Items.ToList().FindIndex(item => item.Id == entryId);
        if (itemIndex < 0)
        {
            return this;
        }

        var item = Items[itemIndex];
        if (item.Marked == marked)
        {
            return this;
        }

        var items = Items.ToArray();
        items[itemIndex] = item with { Marked = marked, MarkedAt = marked ? at : null };
        return this with { Items = items };
    }

    ListSnapshot WithStepMark(Guid taskId, Guid stepId, bool marked, DateTimeOffset at)
    {
        var taskIndex = Tasks.ToList().FindIndex(task => task.Id == taskId);
        if (taskIndex < 0)
        {
            return this;
        }

        var task = Tasks[taskIndex];
        var stepIndex = task.Steps.ToList().FindIndex(step => step.Id == stepId);
        if (stepIndex < 0)
        {
            return this;
        }

        var step = task.Steps[stepIndex];
        if (step.Marked == marked)
        {
            return this;
        }

        var steps = task.Steps.ToArray();
        steps[stepIndex] = step with { Marked = marked, MarkedAt = marked ? at : null };
        var tasks = Tasks.ToArray();
        tasks[taskIndex] = task with { Steps = steps };
        return this with { Tasks = tasks };
    }
}
