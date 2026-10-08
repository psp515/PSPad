using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class DayQueryTests
{
    static readonly DateOnly Today = new(2026, 10, 8);

    [Theory]
    [InlineData("http://x/", "2026-10-08")]
    [InlineData("http://x/?day=2026-10-09", "2026-10-09")]
    [InlineData("http://x/?day=garbage", "2026-10-08")]
    [InlineData("http://x/?task=1&day=2026-10-01", "2026-10-01")]
    public void ItReadsTheDay(string uri, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), DayQuery.From(uri, Today));

    [Fact]
    public void TodayIsTheBarePath() =>
        Assert.Equal("http://x/", DayQuery.For("http://x/?day=2026-10-09", Today, Today));

    [Fact]
    public void AnotherDayGoesInTheQuery() =>
        Assert.Equal("http://x/?day=2026-10-09", DayQuery.For("http://x/", Today.AddDays(1), Today));

    [Fact]
    public void OpeningAndClosingATaskKeepsTheDay()
    {
        var id = Guid.NewGuid();
        var open = TaskQuery.For("http://x/?day=2026-10-09", id);

        Assert.Equal($"http://x/?day=2026-10-09&task={id}", open);
        Assert.Equal("http://x/?day=2026-10-09", TaskQuery.Without(open));
    }
}
