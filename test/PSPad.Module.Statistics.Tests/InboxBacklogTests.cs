using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class InboxBacklogTests
{
    static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

    static readonly DateOnly Today = new(2026, 9, 24);
    static readonly Guid User = Guid.NewGuid();

    static DateTimeOffset Midday(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(2));

    static InboxRecord Record(long id, InboxRecordKind kind, DateOnly day, Guid itemId) =>
        new() { Id = id, UserId = User, At = Midday(day), Kind = kind, ItemId = itemId };

    static IReadOnlySet<Guid> Held(params Guid[] ids) => ids.ToHashSet();

    [Fact]
    public void ThirtyDaysCoverEveryWeekTheWindowTouches()
    {
        var weeks = StatisticsCharts.InboxBacklog([], Today, 30, Zone, Held());

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 6),
                new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 20)
            },
            weeks.Select(week => week.WeekStart));
    }

    [Fact]
    public void AnItemCapturedBeforeTheWindowStillWeighsOnEveryWeekOfIt()
    {
        var weeks = StatisticsCharts.InboxBacklog([], Today, 30, Zone, Held(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(new[] { 2, 2, 2, 2, 2 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void AnItemCapturedBeforeTheWindowAndOrganisedInsideItLeavesTheBacklog()
    {
        var carried = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
            [Record(1, InboxRecordKind.Organised, new DateOnly(2026, 9, 8), carried)],
            Today, 30, Zone, Held(carried));

        Assert.Equal(new[] { 1, 1, 0, 0, 0 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void AnItemCapturedAndDiscardedInsideTheWindowStopsCountingFromThatWeekOn()
    {
        var item = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
        [
            Record(1, InboxRecordKind.Captured, new DateOnly(2026, 8, 31), item),
            Record(2, InboxRecordKind.Discarded, new DateOnly(2026, 9, 15), item)
        ], Today, 30, Zone, Held());

        Assert.Equal(new[] { 0, 1, 1, 0, 0 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void AnItemCapturedAndOrganisedInTheSameWeekNeverSurvivesAWeeksEnd()
    {
        var item = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
        [
            Record(1, InboxRecordKind.Captured, new DateOnly(2026, 9, 7), item),
            Record(2, InboxRecordKind.Organised, new DateOnly(2026, 9, 9), item)
        ], Today, 30, Zone, Held());

        Assert.All(weeks, week => Assert.Equal(0, week.Count));
    }

    [Fact]
    public void TheCurrentWeekIsMeasuredAtTodayNotAtItsSaturday()
    {
        var item = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
            [Record(1, InboxRecordKind.Captured, Today, item)], Today, 30, Zone, Held());

        Assert.Equal(1, weeks[^1].Count);
    }

    [Fact]
    public void RemovingAnItemNobodyEverCapturedIsANoOpNotANegativeBacklog()
    {
        var weeks = StatisticsCharts.InboxBacklog(
        [
            Record(1, InboxRecordKind.Organised, new DateOnly(2026, 9, 8), Guid.NewGuid()),
            Record(2, InboxRecordKind.Discarded, new DateOnly(2026, 9, 9), Guid.NewGuid())
        ], Today, 30, Zone, Held());

        Assert.All(weeks, week => Assert.Equal(0, week.Count));
    }

    [Fact]
    public void ARemovalRepeatedForTheSameItemDoesNotDropTheBacklogTwice()
    {
        var item = Guid.NewGuid();
        var other = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
        [
            Record(1, InboxRecordKind.Organised, new DateOnly(2026, 9, 8), item),
            Record(2, InboxRecordKind.Discarded, new DateOnly(2026, 9, 9), item)
        ], Today, 30, Zone, Held(item, other));

        Assert.Equal(new[] { 2, 2, 1, 1, 1 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void ACaptureCommittedLateStillLandsOnTheWeekItHappened()
    {
        var late = Guid.NewGuid();
        var fresh = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
        [
            Record(9, InboxRecordKind.Captured, new DateOnly(2026, 8, 31), late),
            Record(1, InboxRecordKind.Captured, new DateOnly(2026, 9, 21), fresh)
        ], Today, 30, Zone, Held());

        Assert.Equal(new[] { 0, 1, 1, 1, 2 }, weeks.Select(week => week.Count));
    }

    [Fact]
    public void RecordsOlderThanTheWindowFoldIntoTheOpeningBacklog()
    {
        var early = Guid.NewGuid();

        var weeks = StatisticsCharts.InboxBacklog(
            [Record(1, InboxRecordKind.Captured, Today.AddDays(-60), early)],
            Today, 30, Zone, Held());

        Assert.All(weeks, week => Assert.Equal(1, week.Count));
    }

    [Fact]
    public void AYearLongRangeCoversEveryWeekOfIt()
    {
        var weeks = StatisticsCharts.InboxBacklog([], Today, 365, Zone, Held());

        Assert.Equal(53, weeks.Count);
        Assert.Equal(new DateOnly(2026, 9, 20), weeks[^1].WeekStart);
    }

    [Fact]
    public void ARangeOfNoDaysHasNoWeeks()
    {
        Assert.Empty(StatisticsCharts.InboxBacklog([], Today, 0, Zone, Held()));
    }
}
