using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Module.Tasks.Lists;

public sealed record ListFilter(
    IReadOnlySet<WhenBucket> When,
    IReadOnlySet<Priority> Priorities,
    bool Starred,
    bool HasGoal,
    IReadOnlySet<Guid> GoalIds)
{
    public static ListFilter None { get; } = new(
        new HashSet<WhenBucket>(), new HashSet<Priority>(), false, false, new HashSet<Guid>());

    public int ActiveCount => When.Count + Priorities.Count + (Starred ? 1 : 0) + (HasGoal ? 1 : 0) + GoalIds.Count;

    public bool IsEmpty => ActiveCount == 0;

    bool OtherIsEmpty => !Starred && !HasGoal && GoalIds.Count == 0;

    public static WhenBucket BucketOf(TodoTask task, DateOnly today, DayOfWeek firstDayOfWeek)
    {
        if (TodayRule.DateOf(task, today) is not { } day)
        {
            return WhenBucket.NoDate;
        }

        var weekEnd = today.AddDays(6 - ((int)today.DayOfWeek - (int)firstDayOfWeek + 7) % 7);

        return day < today ? WhenBucket.Overdue
            : day == today ? WhenBucket.Today
            : day <= weekEnd ? WhenBucket.ThisWeek
            : WhenBucket.Later;
    }

    public bool Matches(TodoTask task, DateOnly today, DayOfWeek firstDayOfWeek, IReadOnlyList<Goal> goals) =>
        MatchesWhen(task, today, firstDayOfWeek)
        && (Priorities.Count == 0 || Priorities.Contains(task.Priority))
        && (OtherIsEmpty || MatchesOther(task, goals));

    bool MatchesWhen(TodoTask task, DateOnly today, DayOfWeek firstDayOfWeek)
    {
        if (When.Count == 0)
        {
            return true;
        }

        var bucket = BucketOf(task, today, firstDayOfWeek);
        var done = task.CompletedAt is not null;

        return When.Contains(bucket) && !(done && bucket == WhenBucket.Overdue);
    }

    bool MatchesOther(TodoTask task, IReadOnlyList<Goal> goals)
    {
        var holder = Goal.Holding(goals, task.Id);

        return (Starred && task.Starred)
            || (HasGoal && holder is not null)
            || (holder is not null && GoalIds.Contains(holder.Id));
    }
}
