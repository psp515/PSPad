namespace PSPad.Contracts;

public sealed record StatisticsRecordView(
    long Seq,
    DateTimeOffset At,
    string Kind,
    Guid TaskId,
    string TaskName,
    string? ListName,
    string? GoalName,
    int? CompletionNumber,
    string CurrentStatus);
