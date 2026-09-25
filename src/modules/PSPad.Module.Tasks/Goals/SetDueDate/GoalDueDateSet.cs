using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record GoalDueDateSet(Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly? DueOn)
    : DomainEvent(AggregateId, UserId, At);
