using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StepRenamed(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, string Name)
    : DomainEvent(AggregateId, UserId, At);
