using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Layout;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class SyncStampTests : Bunit.TestContext
{
    static readonly DateTimeOffset Noon = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    readonly GatedSyncTrigger _status = new();
    readonly SwitchableConnectivity _connectivity = new();

    [Fact]
    public void ItSaysWhenTheDataLastArrived()
    {
        _status.LastSyncedAt = Noon.AddMinutes(-2);

        var stamp = RenderStamp();

        Assert.Equal("Updated 2 min ago", stamp.Find(".pspad-sync-stamp").TextContent.Trim());
    }

    [Fact]
    public void ItSaysSoBeforeTheFirstSync()
    {
        var stamp = RenderStamp();

        Assert.Equal("Not synced yet", stamp.Find(".pspad-sync-stamp").TextContent.Trim());
    }

    [Fact]
    public void ItSaysUpdatingWhileASyncRuns()
    {
        _status.LastSyncedAt = Noon.AddMinutes(-2);
        var stamp = RenderStamp();
        _status.Hold();

        _ = _status.SyncNowAsync();

        stamp.WaitForAssertion(() =>
            Assert.Equal("Updating…", stamp.Find(".pspad-sync-stamp").TextContent.Trim()));
    }

    [Fact]
    public void OfflineItKeepsTheLastTimeVisible()
    {
        _status.LastSyncedAt = Noon.AddDays(-3);
        _connectivity.IsOnline = false;

        var stamp = RenderStamp();

        Assert.Equal("Offline · updated 3 days ago", stamp.Find(".pspad-sync-stamp").TextContent.Trim());
    }

    [Fact]
    public void AFailedSyncSaysSoAndKeepsTheLastGoodTime()
    {
        _status.LastSyncedAt = Noon.AddMinutes(-5);
        _status.LastSyncFailed = true;

        var stamp = RenderStamp();

        Assert.Equal("Couldn't update · updated 5 min ago", stamp.Find(".pspad-sync-stamp").TextContent.Trim());
        Assert.Contains("mud-error-text", stamp.Find(".pspad-sync-stamp").ClassList);
    }

    [Fact]
    public void ItFollowsAFinishedSync()
    {
        var stamp = RenderStamp();

        _status.LastSyncedAt = Noon;
        _status.Announce();

        stamp.WaitForAssertion(() =>
            Assert.Equal("Updated just now", stamp.Find(".pspad-sync-stamp").TextContent.Trim()));
    }

    [Fact]
    public void TheExactTimeIsOnHover()
    {
        _status.LastSyncedAt = Noon.AddMinutes(-2);

        var stamp = RenderStamp();

        Assert.Contains("2026", stamp.Find(".pspad-sync-stamp").GetAttribute("title"));
    }

    IRenderedComponent<SyncStamp> RenderStamp()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 10, 7));
        Services.AddSingleton<ISyncTrigger>(_status);
        Services.AddSingleton<ISyncStatus>(_status);
        Services.AddSingleton<IConnectivity>(_connectivity);
        Services.AddSingleton<IClock>(new StoppedClock(Noon));
        return Render<SyncStamp>();
    }

    sealed class StoppedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
