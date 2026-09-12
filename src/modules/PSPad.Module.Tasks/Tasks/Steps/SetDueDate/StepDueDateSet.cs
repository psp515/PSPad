using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StepDueDateSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, DateOnly? DueOn)
    : DomainEvent(AggregateId, UserId, At);
