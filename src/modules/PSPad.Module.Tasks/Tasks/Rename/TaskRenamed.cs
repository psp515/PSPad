using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
