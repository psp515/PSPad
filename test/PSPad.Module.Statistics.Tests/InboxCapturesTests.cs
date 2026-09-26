using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class InboxCapturesTests
{
    static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

    static readonly DateOnly Today = new(2026, 9, 24);
    static readonly Guid User = Guid.NewGuid();

    static DateTimeOffset Midday(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(2));

    static InboxRecord Capture(long id, DateOnly day) =>
        new() { Id = id, UserId = User, At = Midday(day), ItemId = Guid.NewGuid() };

    [Fact]
    public void ThirtyDaysCoverEveryWeekTheWindowTouches()
    {
        var weeks = StatisticsCharts.Captures([], Today, 30, Zone);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 6),
                new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 20)
            },
            weeks.Select(week => week.WeekStart));
    }

    [Fact]
    public void EveryCaptureCountsInTheWeekItHappened()
    {
        var weeks = StatisticsCharts.Captures(
        [
            Capture(1, new DateOnly(2026, 9, 7)),
            Capture(2, new DateOnly(2026, 9, 9)),
            Capture(3, new DateOnly(2026, 9, 21))
        ], Today, 30, Zone);

        Assert.Equal(new[] { 0, 0, 2, 0, 1 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void ACaptureBeforeTheWindowDoesNotLeakIntoItsFirstWeek()
    {
        var weeks = StatisticsCharts.Captures(
        [
            Capture(1, new DateOnly(2026, 8, 25)),
            Capture(2, Today.AddDays(-60))
        ], Today, 30, Zone);

        Assert.All(weeks, week => Assert.Equal(0, week.Count));
    }

    [Fact]
    public void TheFirstWeekCountsOnlyTheDaysInsideTheWindow()
    {
        var weeks = StatisticsCharts.Captures(
        [
            Capture(1, new DateOnly(2026, 8, 25)),
            Capture(2, new DateOnly(2026, 8, 26))
        ], Today, 30, Zone);

        Assert.Equal(new DateOnly(2026, 8, 23), weeks[0].WeekStart);
        Assert.Equal(1, weeks[0].Count);
    }

    [Fact]
    public void TodaysCaptureLandsOnTheCurrentWeek()
    {
        var weeks = StatisticsCharts.Captures([Capture(1, Today)], Today, 30, Zone);

        Assert.Equal(1, weeks[^1].Count);
    }

    [Fact]
    public void WeeksStartOnSundayLikeTheConsistencyGrid()
    {
        var weeks = StatisticsCharts.Captures(
        [
            Capture(1, new DateOnly(2026, 9, 12)),
            Capture(2, new DateOnly(2026, 9, 13))
        ], Today, 30, Zone);

        Assert.Equal(new[] { 0, 0, 1, 1, 0 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void TheDayIsBucketedInTheUsersZoneNotInUtc()
    {
        var lateEvening = new DateTimeOffset(2026, 9, 12, 23, 0, 0, TimeSpan.Zero);

        var weeks = StatisticsCharts.Captures(
            [new InboxRecord { Id = 1, UserId = User, At = lateEvening, ItemId = Guid.NewGuid() }],
            Today, 30, Zone);

        Assert.Equal(new[] { 0, 0, 0, 1, 0 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void ACaptureCommittedLateStillLandsOnTheWeekItHappened()
    {
        var weeks = StatisticsCharts.Captures(
        [
            Capture(9, new DateOnly(2026, 8, 31)),
            Capture(1, new DateOnly(2026, 9, 21))
        ], Today, 30, Zone);

        Assert.Equal(new[] { 0, 1, 0, 0, 1 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void AYearLongRangeCoversEveryWeekOfIt()
    {
        var weeks = StatisticsCharts.Captures([], Today, 365, Zone);

        Assert.Equal(53, weeks.Count);
        Assert.Equal(new DateOnly(2026, 9, 20), weeks[^1].WeekStart);
    }

    [Fact]
    public void ARangeOfNoDaysHasNoWeeks()
    {
        Assert.Empty(StatisticsCharts.Captures([], Today, 0, Zone));
    }
}
