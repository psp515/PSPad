using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxItemOrganised(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId, Guid TaskId, Guid ListId)
    : DomainEvent(AggregateId, UserId, At);
