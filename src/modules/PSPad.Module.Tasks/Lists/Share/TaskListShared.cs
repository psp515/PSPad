using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListShared(Guid AggregateId, Guid UserId, DateTimeOffset At, string Token, string OwnerName)
    : DomainEvent(AggregateId, UserId, At);
