using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public static class ScheduleEntries
{
    public static IReadOnlyList<TodayEntry> For(DayPlan plan, bool showFinished) =>
        showFinished
            ? [.. plan.Scheduled.Concat(plan.Completed.Where(entry => entry.Time is not null)).OrderBy(entry => entry.Time!.Start)]
            : plan.Scheduled;
}
