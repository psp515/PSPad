using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StepAdded(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, string Name, int Position)
    : DomainEvent(AggregateId, UserId, At);
