using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record GoalStatusSet(Guid AggregateId, Guid UserId, DateTimeOffset At, GoalStatus Status)
    : DomainEvent(AggregateId, UserId, At);
