using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskTimeSet(Guid AggregateId, Guid UserId, DateTimeOffset At, TaskTime? Time)
    : DomainEvent(AggregateId, UserId, At);
