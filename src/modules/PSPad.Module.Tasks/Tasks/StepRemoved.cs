using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StepRemoved(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId)
    : DomainEvent(AggregateId, UserId, At);
