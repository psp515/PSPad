using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
