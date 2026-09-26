using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskCompleted(
    Guid AggregateId, Guid UserId, DateTimeOffset At,
    string Name, Guid ListId, Guid? GoalId, DateOnly? DueOn)
    : DomainEvent(AggregateId, UserId, At);
