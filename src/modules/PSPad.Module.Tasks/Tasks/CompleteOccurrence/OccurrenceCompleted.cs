using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record OccurrenceCompleted(
    Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly Day, bool Completed,
    string Name, Guid ListId, Guid? GoalId)
    : DomainEvent(AggregateId, UserId, At);
