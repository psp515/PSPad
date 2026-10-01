using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Ordering;
using PSPad.Module.Tasks.Recurrence;

namespace PSPad.Module.Tasks.Tasks;

public sealed class TodoTask : Aggregate
{
    [JsonInclude]
    public Guid ListId { get; private set; }

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public DateOnly? DueOn { get; private set; }

    [JsonInclude]
    public Guid? GoalId { get; private set; }

    [JsonInclude]
    public Priority Priority { get; private set; } = Priority.None;

    [JsonInclude]
    public bool Starred { get; private set; }

    [JsonInclude]
    public DateTimeOffset? CompletedAt { get; private set; }

    [JsonInclude]
    public DateTimeOffset? CreatedAt { get; private set; }

    [JsonInclude]
    public RecurrenceRule? Recurrence { get; private set; }

    [JsonInclude]
    public LeadTime? LeadTime { get; private set; }

    [JsonInclude]
    public string Description { get; private set; } = "";

    [JsonInclude]
    List<Step> _steps = [];

    [JsonInclude]
    HashSet<DateOnly> _completedDays = [];

    public IReadOnlyList<Step> Steps => _steps.OrderBy(step => step.Position).ToArray();

    public Step? NextUncheckedStep => Steps.FirstOrDefault(step => !step.Checked);

    public IReadOnlySet<DateOnly> CompletedDays => _completedDays;

    public bool IsRecurring => Recurrence is not null;

    public bool OccursOn(DateOnly day) =>
        Recurrence is not null && Recurrence.OccursOn(day) && (DueOn is null || day <= DueOn);

    public bool EndedBy(DateOnly today) => IsRecurring && DueOn < today;

    public static IReadOnlyList<DomainEvent> Decide(
        TodoTask? task, ICommand command, DateTimeOffset at, ListAccess? access = null)
    {
        var grant = access ?? ListAccess.Owner(command.UserId);

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

                return [new TaskCreated(create.TaskId, grant.OwnerId, at, create.ListId, RequireName(create.Name))];

            case RenameTask rename:
                var renaming = Require(task, grant);
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new TaskRenamed(renaming.Id, grant.OwnerId, at, name)];

            case SetTaskDueDate due:
                var dating = Require(task, grant);
                return dating.DueOn == due.DueOn
                    ? []
                    : [new TaskDueDateSet(dating.Id, grant.OwnerId, at, due.DueOn)];

            case SetTaskPriority priority:
                var prioritising = Require(task, grant);
                return prioritising.Priority == priority.Priority
                    ? []
                    : [new TaskPrioritySet(prioritising.Id, grant.OwnerId, at, priority.Priority)];

            case StarTask star:
                var starring = Require(task, grant);
                return starring.Starred == star.Starred
                    ? []
                    : [new TaskStarred(starring.Id, grant.OwnerId, at, star.Starred)];

            case LinkTaskToGoal link:
                var linking = Require(task, grant);
                if (!grant.ByOwner)
                {
                    throw new DomainRejectedException("Only the list's owner links a goal.");
                }

                return linking.GoalId == link.GoalId
                    ? []
                    : [new TaskLinkedToGoal(linking.Id, grant.OwnerId, at, link.GoalId, linking.Name)];

            case MoveTaskToList move:
                var moving = Require(task, grant);
                if (move.ListId == Guid.Empty)
                {
                    throw new DomainRejectedException("A task has to live in a list.");
                }

                return moving.ListId == move.ListId
                    ? []
                    : [new TaskMovedToList(moving.Id, grant.OwnerId, at, move.ListId, moving.Name)];

            case CompleteTask complete:
                var completing = Require(task, grant);
                if (completing.IsRecurring)
                {
                    throw new DomainRejectedException("Tick today's occurrence instead of the whole task.");
                }

                return completing.CompletedAt is not null
                    ? []
                    : [new TaskCompleted(
                        completing.Id, grant.OwnerId, at,
                        completing.Name, completing.ListId, completing.GoalId, completing.DueOn)];

            case ReopenTask reopen:
                var reopening = Require(task, grant);
                return reopening.CompletedAt is null
                    ? []
                    : [new TaskReopened(reopening.Id, grant.OwnerId, at, reopening.Name, reopening.ListId)];

            case DeleteTask delete:
                var deleting = Require(task, grant);
                return deleting.Deleted
                    ? []
                    : [new TaskDeleted(deleting.Id, grant.OwnerId, at, deleting.Name)];

            case AddStep add:
                var adding = Require(task, grant);
                return adding.Steps.Any(step => step.Id == add.StepId)
                    ? []
                    : [new StepAdded(
                        adding.Id, grant.OwnerId, at, add.StepId, RequireName(add.Name),
                        Positions.Next(adding.Steps.Select(step => step.Position)))];

            case RenameStep renameStep:
                var stepRenaming = Require(task, grant);
                var existingStep = RequireStep(stepRenaming, renameStep.StepId);
                var stepName = RequireName(renameStep.Name);
                return existingStep.Name == stepName
                    ? []
                    : [new StepRenamed(stepRenaming.Id, grant.OwnerId, at, renameStep.StepId, stepName)];

            case SetStepDueDate stepDue:
                var stepDating = Require(task, grant);
                var dated = RequireStep(stepDating, stepDue.StepId);
                return dated.DueOn == stepDue.DueOn
                    ? []
                    : [new StepDueDateSet(stepDating.Id, grant.OwnerId, at, stepDue.StepId, stepDue.DueOn)];

            case CheckStep check:
                var checking = Require(task, grant);
                var checkedStep = RequireStep(checking, check.StepId);
                return checkedStep.Checked == check.Checked
                    ? []
                    : [new StepChecked(checking.Id, grant.OwnerId, at, check.StepId, check.Checked)];

            case MoveStep moveStep:
                var stepMoving = Require(task, grant);
                RequireStep(stepMoving, moveStep.StepId);
                var order = Positions.Move(
                    stepMoving.Steps.Select(step => step.Id).ToArray(), moveStep.StepId, moveStep.ToIndex);
                return [new StepsReordered(stepMoving.Id, grant.OwnerId, at, order)];

            case RemoveStep removeStep:
                var removing = Require(task, grant);
                RequireStep(removing, removeStep.StepId);
                return [new StepRemoved(removing.Id, grant.OwnerId, at, removeStep.StepId)];

            case SetTaskRecurrence recurrence:
                var repeating = Require(task, grant);
                if (recurrence.Rule?.Interval is < 0 or > 99)
                {
                    throw new DomainRejectedException("A repeat interval must be between 1 and 99.");
                }

                return repeating.Recurrence == recurrence.Rule
                    ? []
                    : [new TaskRecurrenceSet(repeating.Id, grant.OwnerId, at, recurrence.Rule)];

            case SetTaskLeadTime lead:
                var leading = Require(task, lead.UserId);
                lead.LeadTime?.Validate();
                return leading.LeadTime == lead.LeadTime
                    ? []
                    : [new TaskLeadTimeSet(leading.Id, lead.UserId, at, lead.LeadTime)];

            case SetTaskDescription describe:
                var describing = Require(task, grant);
                var description = (describe.Description ?? "").TrimEnd();
                return describing.Description == description
                    ? []
                    : [new TaskDescriptionSet(describing.Id, grant.OwnerId, at, description)];

            case CompleteOccurrence occurrence:
                var ticking = Require(task, grant);
                if (ticking.Recurrence is null)
                {
                    throw new DomainRejectedException("That task does not repeat.");
                }

                if (occurrence.Completed && !ticking.OccursOn(occurrence.Day))
                {
                    throw new DomainRejectedException("That task does not repeat on that day.");
                }

                return ticking.CompletedDays.Contains(occurrence.Day) == occurrence.Completed
                    ? []
                    : [new OccurrenceCompleted(
                        ticking.Id, grant.OwnerId, at, occurrence.Day, occurrence.Completed,
                        ticking.Name, ticking.ListId, ticking.GoalId)];

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
                CreatedAt = created.At;
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
            case TaskRecurrenceSet recurrenceSet:
                Recurrence = recurrenceSet.Rule;
                if (recurrenceSet.Rule is null)
                {
                    _completedDays.Clear();
                }

                break;
            case TaskLeadTimeSet lead:
                LeadTime = lead.LeadTime;
                break;
            case TaskDescriptionSet described:
                Description = described.Description;
                break;
            case OccurrenceCompleted occurrenceCompleted:
                if (occurrenceCompleted.Completed)
                {
                    _completedDays.Add(occurrenceCompleted.Day);
                }
                else
                {
                    _completedDays.Remove(occurrenceCompleted.Day);
                }

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

    static TodoTask Require(TodoTask? task, ListAccess grant)
    {
        if (task is null || task.Deleted)
        {
            throw new DomainRejectedException("That task no longer exists.");
        }

        if (task.UserId != grant.OwnerId)
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
