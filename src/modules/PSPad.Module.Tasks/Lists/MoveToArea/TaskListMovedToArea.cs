using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListMovedToArea(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid AreaId)
    : DomainEvent(AggregateId, UserId, At);
