namespace PSPad.Module.Tasks.Today;

public sealed record DayPlan(
    IReadOnlyList<TodayEntry> Overdue,
    IReadOnlyList<TodayEntry> Scheduled,
    IReadOnlyList<TodayEntry> AnyTime,
    IReadOnlyList<TodayEntry> Starred,
    IReadOnlyList<TodayEntry> ComingUp,
    IReadOnlyList<TodayEntry> Completed);
