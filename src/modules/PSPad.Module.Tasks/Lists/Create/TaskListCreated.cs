using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListCreated(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid AreaId, string Name, int Position)
    : DomainEvent(AggregateId, UserId, At);
