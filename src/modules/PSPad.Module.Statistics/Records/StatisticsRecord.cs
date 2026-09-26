namespace PSPad.Module.Statistics;

public sealed record StatisticsRecord
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset At { get; init; }
    public RecordKind Kind { get; init; }
    public Guid TaskId { get; init; }
    public string TaskName { get; init; } = "";
    public Guid? ListId { get; init; }
    public Guid? GoalId { get; init; }
    public DateOnly? DueOn { get; init; }
    public DateOnly? OccurrenceDay { get; init; }
    public int? CompletionNumber { get; init; }
}
