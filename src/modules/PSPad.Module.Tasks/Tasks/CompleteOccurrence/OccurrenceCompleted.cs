using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record OccurrenceCompleted(
    Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly Day, bool Completed,
    string Name, Guid ListId)
    : DomainEvent(AggregateId, UserId, At)
{
    /// <summary>
    /// The task's goal on events stored before goal links moved onto the goal (ADR-0065); null on every
    /// newer tick, which statistics attribute to a goal from the reader's own links instead.
    /// </summary>
    public Guid? GoalId { get; init; }
}
