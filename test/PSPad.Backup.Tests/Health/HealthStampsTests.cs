using Microsoft.Extensions.Time.Testing;
using PSPad.Backup.Health;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Health;

[UnitTest]
public sealed class HealthStampsTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("backup-health-").FullName;
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    HealthStamps Stamps() => new(Path.Combine(_root, "started"), Path.Combine(_root, "success"), _clock);

    [Fact]
    public void AContainerThatNeverStartedTheSchedulerIsUnhealthy() => Assert.False(Stamps().IsHealthy());

    [Fact]
    public void AFreshContainerIsHealthyUntilItsFirstRunIsDue()
    {
        var stamps = Stamps();
        stamps.MarkStarted();

        _clock.Advance(TimeSpan.FromHours(25));
        Assert.True(stamps.IsHealthy());

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.False(stamps.IsHealthy());
    }

    [Fact]
    public void ARecentSuccessIsHealthyAndAStaleOneIsNot()
    {
        var stamps = Stamps();
        stamps.MarkStarted();
        _clock.Advance(TimeSpan.FromHours(20));
        stamps.MarkSucceeded();

        _clock.Advance(TimeSpan.FromHours(25));
        Assert.True(stamps.IsHealthy());

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.False(stamps.IsHealthy());
    }
}
