using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record AreaRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name)
    : DomainEvent(AggregateId, UserId, At);
