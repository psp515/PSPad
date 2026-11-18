using PSPad.TestInfrastructure;

namespace PSPad.Module.Reminders.Tests;

[UnitTest]
public class ReminderTtlTests
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private static readonly DateOnly Day = new(2026, 11, 20);

    [Fact]
    public void Lasts_until_the_end_of_the_occurrence_day() =>
        Assert.Equal(TimeSpan.FromHours(15),
            ReminderTtl.For(Day, Warsaw, DateTimeOffset.Parse("2026-11-20T08:00:00Z")));

    [Fact]
    public void Never_drops_below_a_minute() =>
        Assert.Equal(TimeSpan.FromSeconds(60),
            ReminderTtl.For(Day, Warsaw, DateTimeOffset.Parse("2026-11-20T22:59:30Z")));

    [Fact]
    public void Never_exceeds_a_day() =>
        Assert.Equal(TimeSpan.FromSeconds(86400),
            ReminderTtl.For(Day, Warsaw, DateTimeOffset.Parse("2026-11-17T08:00:00Z")));

    [Fact]
    public void After_the_day_ended_is_a_minute() =>
        Assert.Equal(TimeSpan.FromSeconds(60),
            ReminderTtl.For(Day, Warsaw, DateTimeOffset.Parse("2026-11-21T08:00:00Z")));
}
