using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskLinkedToGoal(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid? GoalId)
    : DomainEvent(AggregateId, UserId, At);
