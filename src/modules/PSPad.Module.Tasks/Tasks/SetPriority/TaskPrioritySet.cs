using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskPrioritySet(Guid AggregateId, Guid UserId, DateTimeOffset At, Priority Priority)
    : DomainEvent(AggregateId, UserId, At);
