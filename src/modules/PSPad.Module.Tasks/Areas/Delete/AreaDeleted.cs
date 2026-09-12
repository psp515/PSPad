using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record AreaDeleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
