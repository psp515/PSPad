using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxCreated(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
