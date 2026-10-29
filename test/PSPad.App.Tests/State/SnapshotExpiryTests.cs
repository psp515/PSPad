using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class SnapshotExpiryTests
{
    static readonly DateTimeOffset Midnight = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void APickedDayEndsAtTheNextMidnightInTheUsersZone()
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        var end = SnapshotExpiry.EndOfLocalDay(new DateOnly(2026, 9, 12), newYork);

        Assert.Equal(new DateTimeOffset(2026, 9, 13, 4, 0, 0, TimeSpan.Zero), end);
        Assert.Equal(2, SnapshotExpiry.DaysLeft(end, Midnight));
    }

    [Theory]
    [InlineData(7 * 24 * 60 - 1, 7)]
    [InlineData(6 * 24 * 60, 6)]
    [InlineData(24 * 60 - 1, 1)]
    [InlineData(5 * 60, 1)]
    [InlineData(-60, 1)]
    public void DaysLeftRoundUpAndNeverFallBelowOne(int minutesLeft, int expected) =>
        Assert.Equal(expected, SnapshotExpiry.DaysLeft(Midnight.AddMinutes(minutesLeft), Midnight));
}
