using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListSharingStopped(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
