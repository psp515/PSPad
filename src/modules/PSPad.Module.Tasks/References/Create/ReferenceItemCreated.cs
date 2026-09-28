using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ReferenceItemCreated(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId, string Name, int Position)
    : DomainEvent(AggregateId, UserId, At);
