using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed class TodoTask : Aggregate
{
    public Guid ListId { get; private set; }
    public string Name { get; private set; } = "";
    public DateOnly? DueOn { get; private set; }
    public Guid? GoalId { get; private set; }
    public Priority Priority { get; private set; } = Priority.None;
    public bool Starred { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static IReadOnlyList<DomainEvent> Decide(TodoTask? task, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateTask create:
                if (task is not null)
                {
                    throw new DomainRejectedException("That task already exists.");
                }

                if (create.ListId == Guid.Empty)
                {
                    throw new DomainRejectedException("A task has to live in a list.");
                }

                return [new TaskCreated(create.TaskId, create.UserId, at, create.ListId, RequireName(create.Name))];

            case RenameTask rename:
                var renaming = Require(task, rename.UserId);
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new TaskRenamed(renaming.Id, rename.UserId, at, name)];

            case SetTaskDueDate due:
                var dating = Require(task, due.UserId);
                return dating.DueOn == due.DueOn
                    ? []
                    : [new TaskDueDateSet(dating.Id, due.UserId, at, due.DueOn)];

            case SetTaskPriority priority:
                var prioritising = Require(task, priority.UserId);
                return prioritising.Priority == priority.Priority
                    ? []
                    : [new TaskPrioritySet(prioritising.Id, priority.UserId, at, priority.Priority)];

            case StarTask star:
                var starring = Require(task, star.UserId);
                return starring.Starred == star.Starred
                    ? []
                    : [new TaskStarred(starring.Id, star.UserId, at, star.Starred)];

            case LinkTaskToGoal link:
                var linking = Require(task, link.UserId);
                return linking.GoalId == link.GoalId
                    ? []
                    : [new TaskLinkedToGoal(linking.Id, link.UserId, at, link.GoalId)];

            case MoveTaskToList move:
                var moving = Require(task, move.UserId);
                if (move.ListId == Guid.Empty)
                {
                    throw new DomainRejectedException("A task has to live in a list.");
                }

                return moving.ListId == move.ListId
                    ? []
                    : [new TaskMovedToList(moving.Id, move.UserId, at, move.ListId)];

            case CompleteTask complete:
                var completing = Require(task, complete.UserId);
                return completing.CompletedAt is not null
                    ? []
                    : [new TaskCompleted(completing.Id, complete.UserId, at)];

            case ReopenTask reopen:
                var reopening = Require(task, reopen.UserId);
                return reopening.CompletedAt is null
                    ? []
                    : [new TaskReopened(reopening.Id, reopen.UserId, at)];

            case DeleteTask delete:
                var deleting = Require(task, delete.UserId);
                return deleting.Deleted ? [] : [new TaskDeleted(deleting.Id, delete.UserId, at)];

            default:
                throw new DomainRejectedException($"A task cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case TaskCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                ListId = created.ListId;
                Name = created.Name;
                break;
            case TaskRenamed renamed:
                Name = renamed.Name;
                break;
            case TaskDueDateSet due:
                DueOn = due.DueOn;
                break;
            case TaskPrioritySet priority:
                Priority = priority.Priority;
                break;
            case TaskStarred starred:
                Starred = starred.Starred;
                break;
            case TaskLinkedToGoal linked:
                GoalId = linked.GoalId;
                break;
            case TaskMovedToList moved:
                ListId = moved.ListId;
                break;
            case TaskCompleted completed:
                CompletedAt = completed.At;
                break;
            case TaskReopened:
                CompletedAt = null;
                break;
            case TaskDeleted:
                Deleted = true;
                break;
        }
    }

    static TodoTask Require(TodoTask? task, Guid userId)
    {
        if (task is null || task.Deleted)
        {
            throw new DomainRejectedException("That task no longer exists.");
        }

        if (task.UserId != userId)
        {
            throw new DomainRejectedException("That task belongs to somebody else.");
        }

        return task;
    }

    static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A task needs a name.")
            : name.Trim();
}
