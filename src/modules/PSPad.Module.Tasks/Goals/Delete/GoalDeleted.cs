using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record GoalDeleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
