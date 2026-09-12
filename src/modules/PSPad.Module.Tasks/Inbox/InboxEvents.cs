using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxCreated(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);

public sealed record InboxItemCaptured(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId, string Text, int Position)
    : DomainEvent(AggregateId, UserId, At);

public sealed record InboxItemOrganised(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId, Guid TaskId, Guid ListId)
    : DomainEvent(AggregateId, UserId, At);

public sealed record InboxItemDiscarded(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId)
    : DomainEvent(AggregateId, UserId, At);
