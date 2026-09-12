using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListCreated(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid AreaId, string Name, int Position)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskListRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskListMovedToArea(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid AreaId)
    : DomainEvent(AggregateId, UserId, At);

public sealed record TaskListDeleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
