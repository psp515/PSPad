namespace PSPad.Module.Tasks.Lists;

public sealed record CalendarDay(DateOnly Day, IReadOnlyList<CalendarEntry> Entries);
