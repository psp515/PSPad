using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StepsReordered(Guid AggregateId, Guid UserId, DateTimeOffset At, IReadOnlyList<Guid> Order)
    : DomainEvent(AggregateId, UserId, At);
