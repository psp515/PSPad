using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public static class ScheduleEntries
{
    public static IReadOnlyList<TodayEntry> For(DayPlan plan, DateOnly day, bool showFinished) =>
        showFinished
            ? [.. plan.Scheduled
                .Concat(plan.Completed.Where(entry => entry.Time is not null && entry.DueOn == day))
                .OrderBy(entry => entry.Time!.Start)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)]
            : plan.Scheduled;
}
