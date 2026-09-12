using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskDueDateSet(Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly? DueOn)
    : DomainEvent(AggregateId, UserId, At);
