using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed record AreaCreated(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name, int Position)
    : DomainEvent(AggregateId, UserId, At);
