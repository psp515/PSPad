using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxItemRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId, string Text)
    : DomainEvent(AggregateId, UserId, At);
