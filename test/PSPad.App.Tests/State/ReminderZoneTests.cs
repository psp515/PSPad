using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ReminderZoneTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AStoredZoneBehindTheBrowserIsNamed() =>
        Assert.Equal("Etc/UTC", ReminderZone.Notice("Etc/UTC", "Europe/Warsaw", Now));

    [Fact]
    public void TheSameZoneNeedsNoNotice() =>
        Assert.Null(ReminderZone.Notice("Europe/Warsaw", "Europe/Warsaw", Now));

    [Fact]
    public void AnotherNameForTheSameClockNeedsNoNotice() =>
        Assert.Null(ReminderZone.Notice("Europe/Berlin", "Europe/Warsaw", Now));

    [Fact]
    public void AZoneThatOnlyDiffersInSummerIsNamed() =>
        Assert.Equal("Africa/Lagos", ReminderZone.Notice("Africa/Lagos", "Europe/Warsaw", Now));

    [Fact]
    public void AnUnknownBrowserZoneNeedsNoNotice() =>
        Assert.Null(ReminderZone.Notice("Etc/UTC", "Mars/Olympus", Now));
}
