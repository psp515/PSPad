using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskReopened(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
