using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class RelativeTimeTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(120, "2 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(3 * 60 * 60, "3 hours ago")]
    [InlineData(24 * 60 * 60, "1 day ago")]
    [InlineData(3 * 24 * 60 * 60, "3 days ago")]
    public void ItDescribesHowLongAgo(int secondsAgo, string expected) =>
        Assert.Equal(expected, RelativeTime.Describe(Now.AddSeconds(-secondsAgo), Now));

    [Fact]
    public void ATimeInTheFutureReadsAsJustNow() =>
        Assert.Equal("just now", RelativeTime.Describe(Now.AddMinutes(5), Now));
}
