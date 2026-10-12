using PSPad.Backup.Scheduling;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Scheduling;

[UnitTest]
public class DailyScheduleTests
{
    static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    [Fact]
    public void ALaterTimeTodayRunsToday()
    {
        var schedule = new DailySchedule(new TimeOnly(3, 17), TimeZoneInfo.Utc);

        var next = schedule.NextAfter(new DateTimeOffset(2026, 10, 12, 1, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 3, 17, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void AnEarlierTimeTodayRunsTomorrow()
    {
        var schedule = new DailySchedule(new TimeOnly(3, 17), TimeZoneInfo.Utc);

        var next = schedule.NextAfter(new DateTimeOffset(2026, 10, 12, 3, 17, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 10, 13, 3, 17, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void TheTimeIsReadInTheConfiguredZone()
    {
        var schedule = new DailySchedule(new TimeOnly(3, 17), Warsaw);

        var next = schedule.NextAfter(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 1, 17, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void ATimeSkippedBySpringForwardRunsAnHourLater()
    {
        var schedule = new DailySchedule(new TimeOnly(2, 30), Warsaw);

        var next = schedule.NextAfter(new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void ATimeRepeatedByFallBackRunsOnce()
    {
        var schedule = new DailySchedule(new TimeOnly(2, 30), Warsaw);

        var first = schedule.NextAfter(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        var second = schedule.NextAfter(first);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), first);
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 1, 30, 0, TimeSpan.Zero), second);
    }
}
