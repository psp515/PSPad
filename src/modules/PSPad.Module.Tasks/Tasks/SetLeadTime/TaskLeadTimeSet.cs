using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskLeadTimeSet(Guid AggregateId, Guid UserId, DateTimeOffset At, LeadTime? LeadTime)
    : DomainEvent(AggregateId, UserId, At);
