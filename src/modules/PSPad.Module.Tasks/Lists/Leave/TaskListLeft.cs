using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListLeft(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid MemberId)
    : DomainEvent(AggregateId, UserId, At);
