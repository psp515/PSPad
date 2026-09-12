using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskMovedToList(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId)
    : DomainEvent(AggregateId, UserId, At);
