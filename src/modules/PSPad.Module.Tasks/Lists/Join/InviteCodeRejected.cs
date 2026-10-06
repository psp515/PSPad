using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record InviteCodeRejected(Guid AggregateId, Guid UserId, DateTimeOffset At, int WrongCodes)
    : DomainEvent(AggregateId, UserId, At);
