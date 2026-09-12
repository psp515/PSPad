using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskCompleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
