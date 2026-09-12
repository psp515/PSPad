using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record GoalCreated(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
