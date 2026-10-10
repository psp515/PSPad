using PSPad.Api.Rates;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Rates;

[UnitTest]
public class NbpCacheLifetimeTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void APastDateIsKeptForADay() =>
        Assert.Equal(TimeSpan.FromHours(24), NbpCacheLifetime.For(Today.AddDays(-1), Today));

    [Fact]
    public void TodayIsKeptForAnHour() =>
        Assert.Equal(TimeSpan.FromHours(1), NbpCacheLifetime.For(Today, Today));

    [Fact]
    public void AFutureDateIsKeptForAnHour() =>
        Assert.Equal(TimeSpan.FromHours(1), NbpCacheLifetime.For(Today.AddDays(3), Today));
}
