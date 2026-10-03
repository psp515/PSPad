using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record TaskListJoined(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid MemberId, string DisplayName)
    : DomainEvent(AggregateId, UserId, At);
