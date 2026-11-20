using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskReminderSet(Guid AggregateId, Guid UserId, DateTimeOffset At, TaskReminder? Reminder)
    : DomainEvent(AggregateId, UserId, At);
