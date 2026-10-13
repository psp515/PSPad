using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record TaskRemovedFromGoal(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid TaskId, string TaskName, Guid? MovedTo = null)
    : DomainEvent(AggregateId, UserId, At);
