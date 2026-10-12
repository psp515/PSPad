using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record TaskAddedToGoal(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid TaskId, string TaskName)
    : DomainEvent(AggregateId, UserId, At);
