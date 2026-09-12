using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record OccurrenceCompleted(
    Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly Day, bool Completed)
    : DomainEvent(AggregateId, UserId, At);
