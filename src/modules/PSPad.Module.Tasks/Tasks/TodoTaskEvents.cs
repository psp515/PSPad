using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskCreated(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId, string Name)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskDueDateSet(Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly? DueOn)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskPrioritySet(Guid AggregateId, Guid UserId, DateTimeOffset At, Priority Priority)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskStarred(Guid AggregateId, Guid UserId, DateTimeOffset At, bool Starred)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskLinkedToGoal(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid? GoalId)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskMovedToList(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskCompleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskReopened(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskDeleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);

public sealed record StepAdded(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, string Name, int Position)
    : DomainEvent(AggregateId, UserId, At);

public sealed record StepRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, string Name)
    : DomainEvent(AggregateId, UserId, At);

public sealed record StepDueDateSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, DateOnly? DueOn)
    : DomainEvent(AggregateId, UserId, At);

public sealed record StepChecked(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, bool Checked)
    : DomainEvent(AggregateId, UserId, At);

public sealed record StepsReordered(Guid AggregateId, Guid UserId, DateTimeOffset At, IReadOnlyList<Guid> Order)
    : DomainEvent(AggregateId, UserId, At);

public sealed record StepRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId)
    : DomainEvent(AggregateId, UserId, At);
