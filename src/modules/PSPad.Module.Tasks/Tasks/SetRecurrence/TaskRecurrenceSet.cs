using PSPad.Abstractions;
using PSPad.Module.Tasks.Recurrence;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskRecurrenceSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, RecurrenceRule? Rule)
    : DomainEvent(AggregateId, UserId, At);
