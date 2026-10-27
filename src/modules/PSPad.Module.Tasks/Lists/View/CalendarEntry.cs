namespace PSPad.Module.Tasks.Lists;

public sealed record CalendarEntry(Guid TaskId, bool Overdue, bool Recurring);
