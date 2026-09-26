using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsChartsTests
{
    static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

    static readonly DateOnly Today = new(2026, 9, 24);
    static readonly Guid User = Guid.NewGuid();

    static DateTimeOffset Midday(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(2));

    static StatisticsRecord Record(
        long id,
        RecordKind kind,
        DateOnly day,
        Guid? taskId = null,
        DateOnly? dueOn = null,
        Guid? goalId = null,
        DateOnly? occurrenceDay = null,
        DateTimeOffset? at = null) =>
        new()
        {
            Id = id,
            UserId = User,
            At = at ?? Midday(day),
            Kind = kind,
            TaskId = taskId ?? Guid.NewGuid(),
            TaskName = "task",
            DueOn = dueOn,
            GoalId = goalId,
            OccurrenceDay = occurrenceDay
        };

    static IReadOnlySet<Guid> Open(params Guid[] ids) => ids.ToHashSet();

    static StatisticsLabel Goal(Guid id, string name, bool deleted = false) =>
        new() { Id = id, UserId = User, Kind = LabelKind.Goal, Name = name, Deleted = deleted };

    [Fact]
    public void ACompletionWithADueDateThatDayCountsAsPlanned()
    {
        var series = StatisticsCharts.Completions(
            [Record(1, RecordKind.Completed, Today, dueOn: Today)], Today, 3, Zone);

        Assert.Equal(Today, series[^1].Day);
        Assert.Equal(1, series[^1].Planned);
        Assert.Equal(0, series[^1].Unplanned);
    }

    [Fact]
    public void ACompletionWithNoDueDateCountsAsUnplanned()
    {
        var series = StatisticsCharts.Completions(
            [Record(1, RecordKind.Completed, Today)], Today, 3, Zone);

        Assert.Equal(0, series[^1].Planned);
        Assert.Equal(1, series[^1].Unplanned);
    }

    [Fact]
    public void ACompletionDueLaterStillCountsAsUnplanned()
    {
        var series = StatisticsCharts.Completions(
            [Record(1, RecordKind.Completed, Today, dueOn: Today.AddDays(3))], Today, 3, Zone);

        Assert.Equal(0, series[^1].Planned);
        Assert.Equal(1, series[^1].Unplanned);
    }

    [Fact]
    public void ACompletionAlreadyOverdueCountsAsPlanned()
    {
        var series = StatisticsCharts.Completions(
            [Record(1, RecordKind.Completed, Today, dueOn: Today.AddDays(-4))], Today, 3, Zone);

        Assert.Equal(1, series[^1].Planned);
        Assert.Equal(0, series[^1].Unplanned);
    }

    [Fact]
    public void AnOccurrenceTickAlwaysCountsAsPlanned()
    {
        var series = StatisticsCharts.Completions(
            [Record(1, RecordKind.OccurrenceTicked, Today, occurrenceDay: Today)], Today, 3, Zone);

        Assert.Equal(1, series[^1].Planned);
        Assert.Equal(0, series[^1].Unplanned);
    }

    [Fact]
    public void ATickedThenUntickedOccurrenceCountsAsNothing()
    {
        var task = Guid.NewGuid();

        var series = StatisticsCharts.Completions(
        [
            Record(1, RecordKind.OccurrenceTicked, Today, task, occurrenceDay: Today),
            Record(2, RecordKind.OccurrenceUnticked, Today, task, occurrenceDay: Today)
        ], Today, 3, Zone);

        Assert.Equal(0, series[^1].Planned);
        Assert.Equal(0, series[^1].Unplanned);
    }

    [Fact]
    public void AnOccurrenceTickedAgainAfterAnUntickCountsOnce()
    {
        var task = Guid.NewGuid();

        var series = StatisticsCharts.Completions(
        [
            Record(1, RecordKind.OccurrenceTicked, Today, task, occurrenceDay: Today),
            Record(2, RecordKind.OccurrenceUnticked, Today, task, occurrenceDay: Today),
            Record(3, RecordKind.OccurrenceTicked, Today, task, occurrenceDay: Today)
        ], Today, 3, Zone);

        Assert.Equal(1, series[^1].Planned);
        Assert.Equal(0, series[^1].Unplanned);
    }

    [Fact]
    public void TwoOccurrenceDaysOfTheSameHabitAreResolvedIndependently()
    {
        var task = Guid.NewGuid();
        var yesterday = Today.AddDays(-1);

        var series = StatisticsCharts.Completions(
        [
            Record(1, RecordKind.OccurrenceTicked, yesterday, task, occurrenceDay: yesterday),
            Record(2, RecordKind.OccurrenceTicked, Today, task, occurrenceDay: Today),
            Record(3, RecordKind.OccurrenceUnticked, Today, task, occurrenceDay: Today)
        ], Today, 3, Zone);

        Assert.Equal(1, series[1].Planned);
        Assert.Equal(0, series[2].Planned);
    }

    [Fact]
    public void AnOccurrenceCountsOnItsOccurrenceDayNotTheDayItWasTicked()
    {
        var yesterday = Today.AddDays(-1);
        var afterMidnight = new DateTimeOffset(
            Today.ToDateTime(new TimeOnly(1, 0)), TimeSpan.FromHours(2));

        var series = StatisticsCharts.Completions(
        [
            Record(1, RecordKind.OccurrenceTicked, Today, occurrenceDay: yesterday, at: afterMidnight)
        ], Today, 3, Zone);

        Assert.Equal(yesterday, series[1].Day);
        Assert.Equal(1, series[1].Planned);
        Assert.Equal(0, series[2].Planned);
    }

    [Fact]
    public void DaysAreBucketedInTheUsersZoneNotUtc()
    {
        var lateInUtc = new DateTimeOffset(2026, 9, 23, 23, 30, 0, TimeSpan.Zero);

        var series = StatisticsCharts.Completions(
            [Record(1, RecordKind.Completed, Today, at: lateInUtc)], Today, 3, Zone);

        Assert.Equal(new DateOnly(2026, 9, 23), series[1].Day);
        Assert.Equal(0, series[1].Unplanned);
        Assert.Equal(1, series[2].Unplanned);
    }

    [Fact]
    public void OpenedCountsCreatedRecordsPerDayAndIgnoresOtherKinds()
    {
        var series = StatisticsCharts.Opened(
        [
            Record(1, RecordKind.Created, Today.AddDays(-2)),
            Record(2, RecordKind.Created, Today),
            Record(3, RecordKind.Created, Today),
            Record(4, RecordKind.Completed, Today)
        ], Today, 3, Zone);

        Assert.Equal(new[] { 1, 0, 2 }, series.Select(point => point.Count));
    }

    [Fact]
    public void OpenedIgnoresRecordsOlderThanTheWindow()
    {
        var series = StatisticsCharts.Opened(
        [
            Record(1, RecordKind.Created, Today.AddDays(-40)),
            Record(2, RecordKind.Created, Today)
        ], Today, 3, Zone);

        Assert.Equal(1, series.Sum(point => point.Count));
        Assert.Equal(1, series[^1].Count);
    }

    [Fact]
    public void OutstandingStartsFromTheOpeningBalanceNotZero()
    {
        var series = StatisticsCharts.Outstanding(
            [], Today, 3, Zone,
            Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(new[] { 5, 5, 5 }, series.Select(point => point.Count));
    }

    [Fact]
    public void OutstandingRisesOnCreateAndFallsOnCompleteAndDelete()
    {
        var finished = Guid.NewGuid();
        var dropped = Guid.NewGuid();

        var series = StatisticsCharts.Outstanding(
        [
            Record(1, RecordKind.Created, Today.AddDays(-2), finished),
            Record(2, RecordKind.Created, Today.AddDays(-2), dropped),
            Record(3, RecordKind.Completed, Today.AddDays(-1), finished),
            Record(4, RecordKind.Deleted, Today, dropped)
        ], Today, 3, Zone, Open());

        Assert.Equal(new[] { 2, 1, 0 }, series.Select(point => point.Count));
    }

    [Fact]
    public void ReopeningATaskPutsItBackOnTheOutstandingLine()
    {
        var task = Guid.NewGuid();

        var series = StatisticsCharts.Outstanding(
        [
            Record(1, RecordKind.Created, Today.AddDays(-2), task),
            Record(2, RecordKind.Completed, Today.AddDays(-1), task),
            Record(3, RecordKind.Reopened, Today, task)
        ], Today, 3, Zone, Open());

        Assert.Equal(new[] { 1, 0, 1 }, series.Select(point => point.Count));
    }

    [Fact]
    public void OutstandingFoldsRecordsOlderThanTheWindowIntoTheOpeningBalance()
    {
        var series = StatisticsCharts.Outstanding(
        [
            Record(1, RecordKind.Created, Today.AddDays(-9)),
            Record(2, RecordKind.Created, Today)
        ], Today, 3, Zone,
            Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(new[] { 5, 5, 6 }, series.Select(point => point.Count));
    }

    [Fact]
    public void OutstandingAppliesARecordOnItsOwnDayNotAfterEveryLowerSeq()
    {
        var carried = Guid.NewGuid();
        var completedOffline = Guid.NewGuid();

        StatisticsRecord[] records =
        [
            Record(1, RecordKind.Created, Today.AddDays(-3), carried),
            Record(2, RecordKind.Created, Today),
            Record(3, RecordKind.Completed, Today.AddDays(-3), completedOffline)
        ];

        var series = StatisticsCharts.Outstanding(records, Today, 4, Zone, Open(completedOffline));

        Assert.Equal(new[] { 1, 1, 1, 2 }, series.Select(point => point.Count));
    }

    [Fact]
    public void ATaskCompletedThenDeletedLeavesTheLineOnlyOnce()
    {
        var task = Guid.NewGuid();

        var series = StatisticsCharts.Outstanding(
        [
            Record(1, RecordKind.Created, Today.AddDays(-2), task),
            Record(2, RecordKind.Completed, Today.AddDays(-1), task),
            Record(3, RecordKind.Deleted, Today, task)
        ], Today, 3, Zone, Open());

        Assert.Equal(new[] { 1, 0, 0 }, series.Select(point => point.Count));
    }

    [Fact]
    public void ATaskDeletedWhileStillOpenLeavesTheLineExactlyOnce()
    {
        var dropped = Guid.NewGuid();
        var kept = Guid.NewGuid();

        var series = StatisticsCharts.Outstanding(
        [
            Record(1, RecordKind.Created, Today.AddDays(-2), dropped),
            Record(2, RecordKind.Created, Today.AddDays(-2), kept),
            Record(3, RecordKind.Deleted, Today.AddDays(-1), dropped)
        ], Today, 3, Zone, Open());

        Assert.Equal(new[] { 2, 1, 1 }, series.Select(point => point.Count));
    }

    [Fact]
    public void CompletingATaskOpenedBeforeTheWindowDropsTheLine()
    {
        var task = Guid.NewGuid();
        var untouched = Guid.NewGuid();

        var series = StatisticsCharts.Outstanding(
            [Record(1, RecordKind.Completed, Today.AddDays(-1), task)],
            Today, 3, Zone, Open(task, untouched));

        Assert.Equal(new[] { 2, 1, 1 }, series.Select(point => point.Count));
    }

    [Fact]
    public void DeletingATaskThatWasNeverOpenIsANoOp()
    {
        var stranger = Guid.NewGuid();

        var series = StatisticsCharts.Outstanding(
            [Record(1, RecordKind.Deleted, Today.AddDays(-1), stranger)],
            Today, 3, Zone, Open(Guid.NewGuid()));

        Assert.Equal(new[] { 1, 1, 1 }, series.Select(point => point.Count));
    }

    [Fact]
    public void DeletingATaskLeavesEarlierDaysUntouched()
    {
        var task = Guid.NewGuid();
        var goal = Guid.NewGuid();
        var born = Today.AddDays(-2);
        var finished = Today.AddDays(-1);

        StatisticsRecord[] alive =
        [
            Record(1, RecordKind.Created, born, task),
            Record(2, RecordKind.Completed, finished, task, dueOn: finished, goalId: goal)
        ];

        StatisticsRecord[] deleted = [.. alive, Record(3, RecordKind.Deleted, Today, task)];

        var completions = StatisticsCharts.Completions(deleted, Today, 3, Zone);
        var heatmap = StatisticsCharts.Heatmap(deleted, Today, 3, Zone);
        var bars = StatisticsCharts.ByGoal(deleted, [Goal(goal, "Fitness")]);
        var outstanding = StatisticsCharts.Outstanding(deleted, Today, 3, Zone, Open());
        var whileAlive = StatisticsCharts.Outstanding(alive, Today, 3, Zone, Open());

        Assert.Equal(new[] { 0, 1, 0 }, completions.Select(point => point.Planned));
        Assert.Equal(new[] { 0, 0, 0 }, completions.Select(point => point.Unplanned));
        Assert.Equal(new[] { 0, 1, 0 }, heatmap.Select(point => point.Count));
        Assert.Equal(1, Assert.Single(bars, bar => bar.GoalId == goal).Count);
        Assert.Equal(new[] { 1, 0, 0 }, outstanding.Select(point => point.Count));
        Assert.Equal(whileAlive, outstanding);
    }

    [Fact]
    public void HeatmapCountsPlannedAndUnplannedTogether()
    {
        var heatmap = StatisticsCharts.Heatmap(
        [
            Record(1, RecordKind.Completed, Today, dueOn: Today),
            Record(2, RecordKind.Completed, Today),
            Record(3, RecordKind.OccurrenceTicked, Today, occurrenceDay: Today),
            Record(4, RecordKind.Created, Today)
        ], Today, 3, Zone);

        Assert.Equal(new[] { 0, 0, 3 }, heatmap.Select(point => point.Count));
    }

    [Fact]
    public void TasksWithNoGoalGetTheirOwnBar()
    {
        var goal = Guid.NewGuid();

        var bars = StatisticsCharts.ByGoal(
        [
            Record(1, RecordKind.Completed, Today, goalId: goal),
            Record(2, RecordKind.Completed, Today),
            Record(3, RecordKind.Completed, Today)
        ], [Goal(goal, "Fitness")]);

        Assert.Equal(2, bars.Count);
        Assert.Null(bars[0].GoalId);
        Assert.Equal("No goal", bars[0].Name);
        Assert.Equal(2, bars[0].Count);
        Assert.Equal(goal, bars[1].GoalId);
        Assert.Equal("Fitness", bars[1].Name);
        Assert.Equal(1, bars[1].Count);
    }

    [Fact]
    public void TheNoGoalBarIsThereEvenWhenEveryTaskHasAGoal()
    {
        var goal = Guid.NewGuid();

        var bars = StatisticsCharts.ByGoal(
            [Record(1, RecordKind.Completed, Today, goalId: goal)], [Goal(goal, "Fitness")]);

        var noGoal = Assert.Single(bars, bar => bar.GoalId is null);
        Assert.Equal(0, noGoal.Count);
    }

    [Fact]
    public void ADeletedGoalStillHasItsNameInTheBars()
    {
        var goal = Guid.NewGuid();

        var bars = StatisticsCharts.ByGoal(
            [Record(1, RecordKind.Completed, Today, goalId: goal)],
            [Goal(goal, "Fitness", deleted: true)]);

        var bar = Assert.Single(bars, candidate => candidate.GoalId == goal);
        Assert.Equal("Fitness", bar.Name);
        Assert.Equal(1, bar.Count);
    }

    [Fact]
    public void BarsAreRankedByCount()
    {
        var small = Guid.NewGuid();
        var large = Guid.NewGuid();

        var bars = StatisticsCharts.ByGoal(
        [
            Record(1, RecordKind.Completed, Today, goalId: small),
            Record(2, RecordKind.Completed, Today, goalId: large),
            Record(3, RecordKind.Completed, Today, goalId: large)
        ], [Goal(small, "Small"), Goal(large, "Large")]);

        Assert.Equal(new[] { "Large", "Small", "No goal" }, bars.Select(bar => bar.Name));
    }

    [Fact]
    public void TheRangeAlwaysHasOnePointPerDayEvenWhereNothingHappened()
    {
        var completions = StatisticsCharts.Completions([], Today, 30, Zone);
        var opened = StatisticsCharts.Opened([], Today, 30, Zone);
        var outstanding = StatisticsCharts.Outstanding([], Today, 30, Zone, Open());
        var heatmap = StatisticsCharts.Heatmap([], Today, 30, Zone);

        Assert.Equal(30, completions.Count);
        Assert.Equal(30, opened.Count);
        Assert.Equal(30, outstanding.Count);
        Assert.Equal(30, heatmap.Count);

        Assert.Equal(Today.AddDays(-29), completions[0].Day);
        Assert.Equal(Today, completions[^1].Day);
        Assert.Equal(
            Enumerable.Range(0, 30).Select(offset => Today.AddDays(offset - 29)),
            heatmap.Select(point => point.Day));
        Assert.All(completions, point => Assert.Equal(0, point.Planned + point.Unplanned));
        Assert.All(opened, point => Assert.Equal(0, point.Count));
    }

    [Fact]
    public void TilesCountTodayTheWeekAndTheNetChangeOverTheRange()
    {
        var carried = Guid.NewGuid();
        var older = Guid.NewGuid();
        var finished = Guid.NewGuid();
        var fresh = Guid.NewGuid();

        StatisticsRecord[] records =
        [
            Record(1, RecordKind.Created, Today.AddDays(-3), carried),
            Record(2, RecordKind.Created, Today, finished),
            Record(3, RecordKind.Created, Today, fresh),
            Record(4, RecordKind.Completed, Today, finished, dueOn: Today),
            Record(5, RecordKind.Completed, Today.AddDays(-3), older),
            Record(6, RecordKind.OccurrenceTicked, Today, occurrenceDay: Today)
        ];

        var outstanding = StatisticsCharts.Outstanding(records, Today, 7, Zone, Open(older));
        var tiles = StatisticsCharts.Tiles(records, outstanding, Today, Zone);

        Assert.Equal(new[] { 1, 1, 1, 1, 1, 1, 2 }, outstanding.Select(point => point.Count));
        Assert.Equal(2, tiles.DoneToday);
        Assert.Equal(2, tiles.OpenedToday);
        Assert.Equal(3, tiles.DoneThisWeek);
        Assert.Equal(1, tiles.NetChange);
    }

    [Fact]
    public void TheNetChangeTileIsTheOutstandingLinesOwnDelta()
    {
        var kept = Guid.NewGuid();

        StatisticsRecord[] records =
        [
            Record(1, RecordKind.Created, Today.AddDays(-2), kept),
            Record(2, RecordKind.Completed, Today, kept)
        ];

        var outstanding = StatisticsCharts.Outstanding(records, Today, 3, Zone, Open());
        var tiles = StatisticsCharts.Tiles(records, outstanding, Today, Zone);

        Assert.Equal(outstanding[^1].Count - outstanding[0].Count, tiles.NetChange);
        Assert.Equal(-1, tiles.NetChange);
    }

    [Fact]
    public void TilesDoNotCountAnOccurrenceTheUserTookBack()
    {
        var task = Guid.NewGuid();

        var tiles = StatisticsCharts.Tiles(
        [
            Record(1, RecordKind.OccurrenceTicked, Today, task, occurrenceDay: Today),
            Record(2, RecordKind.OccurrenceUnticked, Today, task, occurrenceDay: Today)
        ], [], Today, Zone);

        Assert.Equal(0, tiles.DoneToday);
        Assert.Equal(0, tiles.DoneThisWeek);
    }

    [Fact]
    public void TilesBucketTodayInTheUsersZoneNotUtc()
    {
        var lateInUtc = new DateTimeOffset(2026, 9, 23, 23, 30, 0, TimeSpan.Zero);

        var tiles = StatisticsCharts.Tiles(
        [
            Record(1, RecordKind.Created, Today, at: lateInUtc),
            Record(2, RecordKind.Completed, Today, at: lateInUtc)
        ], [], Today, Zone);

        Assert.Equal(1, tiles.OpenedToday);
        Assert.Equal(1, tiles.DoneToday);
        Assert.Equal(1, tiles.DoneThisWeek);
    }

    [Fact]
    public void OpenedBucketsInTheUsersZoneNotUtc()
    {
        var lateInUtc = new DateTimeOffset(2026, 9, 23, 23, 30, 0, TimeSpan.Zero);

        var series = StatisticsCharts.Opened(
            [Record(1, RecordKind.Created, Today, at: lateInUtc)], Today, 3, Zone);

        Assert.Equal(new DateOnly(2026, 9, 23), series[1].Day);
        Assert.Equal(new[] { 0, 0, 1 }, series.Select(point => point.Count));
    }

    [Fact]
    public void OutstandingBucketsInTheUsersZoneNotUtc()
    {
        var lateInUtc = new DateTimeOffset(2026, 9, 23, 23, 30, 0, TimeSpan.Zero);

        var series = StatisticsCharts.Outstanding(
            [Record(1, RecordKind.Created, Today, at: lateInUtc)], Today, 3, Zone, Open());

        Assert.Equal(new[] { 0, 0, 1 }, series.Select(point => point.Count));
    }

    [Fact]
    public void TheLatestOccurrenceRecordIsTheHighestIdNotTheLastSeen()
    {
        var task = Guid.NewGuid();
        var early = new DateTimeOffset(Today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(2));
        var late = new DateTimeOffset(Today.ToDateTime(new TimeOnly(11, 0)), TimeSpan.FromHours(2));

        var series = StatisticsCharts.Completions(
        [
            Record(3, RecordKind.OccurrenceTicked, Today, task, occurrenceDay: Today, at: early),
            Record(2, RecordKind.OccurrenceUnticked, Today, task, occurrenceDay: Today, at: late)
        ], Today, 3, Zone);

        Assert.Equal(1, series[^1].Planned);
    }

    [Fact]
    public void AGoalLinkedHabitsTicksCountTowardsItsBar()
    {
        var goal = Guid.NewGuid();
        var habit = Guid.NewGuid();

        var bars = StatisticsCharts.ByGoal(
        [
            Record(1, RecordKind.Completed, Today, goalId: goal),
            Record(2, RecordKind.OccurrenceTicked, Today.AddDays(-1), habit,
                goalId: goal, occurrenceDay: Today.AddDays(-1)),
            Record(3, RecordKind.OccurrenceTicked, Today, habit,
                goalId: goal, occurrenceDay: Today)
        ], [Goal(goal, "Fitness")]);

        Assert.Equal(3, Assert.Single(bars, bar => bar.GoalId == goal).Count);
    }

    [Fact]
    public void AGoalLinkedHabitDayTheUserTookBackDoesNotCountTowardsItsBar()
    {
        var goal = Guid.NewGuid();
        var habit = Guid.NewGuid();

        var bars = StatisticsCharts.ByGoal(
        [
            Record(1, RecordKind.Completed, Today, goalId: goal),
            Record(2, RecordKind.OccurrenceTicked, Today, habit,
                goalId: goal, occurrenceDay: Today),
            Record(3, RecordKind.OccurrenceUnticked, Today, habit,
                goalId: goal, occurrenceDay: Today)
        ], [Goal(goal, "Fitness")]);

        Assert.Equal(1, Assert.Single(bars, bar => bar.GoalId == goal).Count);
    }
}
