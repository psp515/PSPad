namespace PSPad.Module.Tasks.Today;

public sealed record TodayEntry(
    Guid TaskId,
    Guid ListId,
    string Name,
    bool Overdue,
    DateOnly? DueOn,
    bool Recurring,
    bool Starred,
    int Priority);
