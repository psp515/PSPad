using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskLinkedToGoal(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid? GoalId, string Name)
    : DomainEvent(AggregateId, UserId, At);
