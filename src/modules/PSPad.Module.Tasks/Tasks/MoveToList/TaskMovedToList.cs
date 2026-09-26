using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskMovedToList(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId, string Name)
    : DomainEvent(AggregateId, UserId, At);
