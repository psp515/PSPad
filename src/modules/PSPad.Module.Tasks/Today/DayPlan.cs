namespace PSPad.Module.Tasks.Today;

public sealed record DayPlan(
    IReadOnlyList<TodayEntry> Overdue,
    IReadOnlyList<TodayEntry> Today,
    IReadOnlyList<TodayEntry> Starred,
    IReadOnlyList<TodayEntry> Tomorrow,
    IReadOnlyList<TodayEntry> Upcoming,
    IReadOnlyList<TodayEntry> Completed);
