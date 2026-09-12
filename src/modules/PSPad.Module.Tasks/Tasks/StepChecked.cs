using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record StepChecked(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid StepId, bool Checked)
    : DomainEvent(AggregateId, UserId, At);
