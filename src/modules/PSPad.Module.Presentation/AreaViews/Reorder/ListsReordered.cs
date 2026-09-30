using PSPad.Abstractions;

namespace PSPad.Module.Presentation.AreaViews;

public sealed record ListsReordered(
    Guid AggregateId,
    Guid UserId,
    DateTimeOffset At,
    Guid AreaId,
    IReadOnlyList<Guid> Order)
    : DomainEvent(AggregateId, UserId, At);
