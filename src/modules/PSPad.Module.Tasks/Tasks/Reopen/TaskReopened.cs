using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskReopened(Guid AggregateId, Guid UserId, DateTimeOffset At, string Name, Guid ListId)
    : DomainEvent(AggregateId, UserId, At);
