using PSPad.Abstractions;

namespace PSPad.Module.Presentation.ListViews;

public sealed record ListPlaced(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId, Guid? AreaId)
    : DomainEvent(AggregateId, UserId, At);
