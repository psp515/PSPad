using PSPad.Abstractions;
using PSPad.Module.Tasks.Ordering;

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

    readonly List<Step> _steps = [];

    public IReadOnlyList<Step> Steps => _steps.OrderBy(step => step.Position).ToArray();

    public Step? NextUncheckedStep => Steps.FirstOrDefault(step => !step.Checked);

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

            case AddStep add:
                var adding = Require(task, add.UserId);
                return adding.Steps.Any(step => step.Id == add.StepId)
                    ? []
                    : [new StepAdded(
                        adding.Id, add.UserId, at, add.StepId, RequireName(add.Name),
                        Positions.Next(adding.Steps.Select(step => step.Position)))];

            case RenameStep renameStep:
                var stepRenaming = Require(task, renameStep.UserId);
                var existingStep = RequireStep(stepRenaming, renameStep.StepId);
                var stepName = RequireName(renameStep.Name);
                return existingStep.Name == stepName
                    ? []
                    : [new StepRenamed(stepRenaming.Id, renameStep.UserId, at, renameStep.StepId, stepName)];

            case SetStepDueDate stepDue:
                var stepDating = Require(task, stepDue.UserId);
                var dated = RequireStep(stepDating, stepDue.StepId);
                return dated.DueOn == stepDue.DueOn
                    ? []
                    : [new StepDueDateSet(stepDating.Id, stepDue.UserId, at, stepDue.StepId, stepDue.DueOn)];

            case CheckStep check:
                var checking = Require(task, check.UserId);
                var checkedStep = RequireStep(checking, check.StepId);
                return checkedStep.Checked == check.Checked
                    ? []
                    : [new StepChecked(checking.Id, check.UserId, at, check.StepId, check.Checked)];

            case MoveStep moveStep:
                var stepMoving = Require(task, moveStep.UserId);
                RequireStep(stepMoving, moveStep.StepId);
                var order = Positions.Move(
                    stepMoving.Steps.Select(step => step.Id).ToArray(), moveStep.StepId, moveStep.ToIndex);
                return [new StepsReordered(stepMoving.Id, moveStep.UserId, at, order)];

            case RemoveStep removeStep:
                var removing = Require(task, removeStep.UserId);
                RequireStep(removing, removeStep.StepId);
                return [new StepRemoved(removing.Id, removeStep.UserId, at, removeStep.StepId)];

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
            case StepAdded added:
                _steps.Add(new Step(added.StepId, added.Name, null, false, added.Position));
                break;
            case StepRenamed stepRenamed:
                Replace(stepRenamed.StepId, step => step with { Name = stepRenamed.Name });
                break;
            case StepDueDateSet stepDue:
                Replace(stepDue.StepId, step => step with { DueOn = stepDue.DueOn });
                break;
            case StepChecked stepChecked:
                Replace(stepChecked.StepId, step => step with { Checked = stepChecked.Checked });
                break;
            case StepsReordered reordered:
                for (var index = 0; index < reordered.Order.Count; index++)
                {
                    Replace(reordered.Order[index], step => step with { Position = index });
                }

                break;
            case StepRemoved stepRemoved:
                _steps.RemoveAll(step => step.Id == stepRemoved.StepId);
                Densify();
                break;
        }
    }

    void Replace(Guid stepId, Func<Step, Step> change)
    {
        var index = _steps.FindIndex(step => step.Id == stepId);
        if (index >= 0)
        {
            _steps[index] = change(_steps[index]);
        }
    }

    void Densify()
    {
        var ordered = _steps.OrderBy(step => step.Position).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            Replace(ordered[index].Id, step => step with { Position = index });
        }
    }

    static Step RequireStep(TodoTask task, Guid stepId) =>
        task.Steps.FirstOrDefault(step => step.Id == stepId)
        ?? throw new DomainRejectedException("That step is not on this task.");

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
