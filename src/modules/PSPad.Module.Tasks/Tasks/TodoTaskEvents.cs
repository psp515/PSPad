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
