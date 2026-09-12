using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxItemCaptured(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ItemId, string Text, int Position)
    : DomainEvent(AggregateId, UserId, At);
