using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxItemDiscarded(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId)
    : DomainEvent(AggregateId, UserId, At);
