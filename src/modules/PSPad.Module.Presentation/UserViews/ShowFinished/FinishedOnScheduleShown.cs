using PSPad.Abstractions;

namespace PSPad.Module.Presentation.UserViews;

public sealed record FinishedOnScheduleShown(Guid AggregateId, Guid UserId, DateTimeOffset At, bool Show)
    : DomainEvent(AggregateId, UserId, At);
