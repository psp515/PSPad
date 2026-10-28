using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PSPad.App.Live;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Live;

[UnitTest]
public class LiveUpdatesTests
{
    const string Ready = "event: ready\ndata: ready\n\n";
    const string Sync = "event: sync\ndata: sync\n\n";

    readonly ScriptedLiveStream _stream = new();
    readonly GatedSyncTrigger _sync = new();
    readonly SwitchableConnectivity _connectivity = new();
    readonly SwitchableVisibility _visibility = new();
    readonly List<TimeSpan> _waits = [];

    LiveUpdates Live(TimeSpan? silence = null, ILogger<LiveUpdates>? logger = null) => new(
        _stream, _sync, _connectivity, _visibility,
        new LiveTiming(silence ?? TimeSpan.FromMinutes(1), () => 1.0, (delay, _) =>
        {
            _waits.Add(delay);
            return Task.CompletedTask;
        }),
        logger ?? NullLogger<LiveUpdates>.Instance);

    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "condition not met within 5 s");
            await Task.Delay(10);
        }
    }

    static FeedStream Feed(params string[] frames)
    {
        var feed = new FeedStream();
        foreach (var frame in frames)
        {
            feed.Send(frame);
        }

        return feed;
    }

    [Fact]
    public async Task FailuresBackOffDoublingUpToAMinute()
    {
        for (var i = 0; i < 8; i++)
        {
            _stream.ThenFail();
        }

        await using var live = Live();

        live.Start();

        await Until(() => _waits.Count == 8);
        Assert.Equal(new[] { 1, 2, 4, 8, 16, 32, 60, 60 }.Select(seconds => TimeSpan.FromSeconds(seconds)), _waits);
    }

    [Fact]
    public async Task AStreamThatReachedReadyResetsTheBackoff()
    {
        var dropped = Feed(Ready);
        _stream.ThenFail().ThenFail().ThenFeed(dropped).ThenFail();
        await using var live = Live();

        live.Start();
        await Until(() => _sync.Calls == 1);
        dropped.Fail();

        await Until(() => _waits.Count == 4);
        Assert.Equal(new[] { 1, 2, 1, 2 }.Select(seconds => TimeSpan.FromSeconds(seconds)), _waits);
    }

    [Fact]
    public async Task ACleanEndReconnectsWithoutWaiting()
    {
        var first = Feed(Ready);
        first.End();
        _stream.ThenFeed(first);
        await using var live = Live();

        live.Start();

        await Until(() => _stream.Opens == 2);
        Assert.Empty(_waits);
    }

    [Fact]
    public async Task OfflineHoldsTheConnectionUntilOnline()
    {
        _connectivity.IsOnline = false;
        _stream.ThenFeed(Feed(Ready));
        await using var live = Live();

        live.Start();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, _stream.Opens);

        _connectivity.GoOnline();

        await Until(() => _stream.Opens == 1);
    }

    [Fact]
    public async Task HidingThePageDropsTheStreamAndShowingItReconnects()
    {
        _stream.ThenFeed(Feed(Ready)).ThenFeed(Feed(Ready));
        await using var live = Live();
        live.Start();
        await Until(() => _sync.Calls == 1);

        _visibility.Hide();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(1, _stream.Opens);

        _visibility.Show();

        await Until(() => _stream.Opens == 2);
        Assert.Empty(_waits);
    }

    [Fact]
    public async Task GoingOfflineDropsTheStream()
    {
        _stream.ThenFeed(Feed(Ready)).ThenFeed(Feed(Ready));
        await using var live = Live();
        live.Start();
        await Until(() => _sync.Calls == 1);

        _connectivity.GoOffline();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        _connectivity.GoOnline();

        await Until(() => _stream.Opens == 2);
    }

    [Fact]
    public async Task SilenceCountsAsAFailureAndReconnects()
    {
        _stream.ThenFeed(Feed(Ready));
        await using var live = Live(silence: TimeSpan.FromMilliseconds(200));

        live.Start();

        await Until(() => _stream.Opens == 2);
        Assert.Equal([TimeSpan.FromSeconds(1)], _waits);
    }

    [Fact]
    public async Task StartingTwiceRunsOneLoop()
    {
        await using var live = Live();

        live.Start();
        live.Start();

        await Until(() => _stream.Opens == 1);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(1, _stream.Opens);
    }

    [Fact]
    public async Task ACleanEndBeforeReadyBacksOff()
    {
        var first = new FeedStream();
        first.End();
        var second = new FeedStream();
        second.End();
        _stream.ThenFeed(first).ThenFeed(second);
        await using var live = Live();

        live.Start();

        await Until(() => _waits.Count == 2);
        Assert.Equal(new[] { 1, 2 }.Select(seconds => TimeSpan.FromSeconds(seconds)), _waits);
    }

    [Fact]
    public async Task AnUnexpectedErrorIsLoggedAsAWarning()
    {
        var error = new InvalidOperationException("no access token");
        _stream.Then(_ => Task.FromException<Stream>(error)).ThenFeed(Feed(Ready));
        var logger = new RecordingLogger<LiveUpdates>();
        await using var live = Live(logger: logger);

        live.Start();

        await Until(() => _sync.Calls == 1);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(error, entry.Exception);
        Assert.Equal([TimeSpan.FromSeconds(1)], _waits);
    }

    [Fact]
    public async Task ExpectedFailuresAreNotLogged()
    {
        var dropped = Feed(Ready);
        _stream.ThenFail().ThenFeed(dropped);
        var logger = new RecordingLogger<LiveUpdates>();
        await using var live = Live(logger: logger);

        live.Start();
        await Until(() => _sync.Calls == 1);
        dropped.Fail();
        await Until(() => _stream.Opens == 3);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AnyOpeningErrorBacksOffAndRetries()
    {
        _stream.Then(_ => Task.FromException<Stream>(new InvalidOperationException("no access token")))
            .ThenFeed(Feed(Ready));
        await using var live = Live();

        live.Start();

        await Until(() => _sync.Calls == 1);
        Assert.Equal([TimeSpan.FromSeconds(1)], _waits);
    }

    [Fact]
    public async Task HeartbeatsKeepASilentStreamAlive()
    {
        var feed = Feed(Ready);
        _stream.ThenFeed(feed);
        await using var live = Live(silence: TimeSpan.FromSeconds(1));

        live.Start();
        await Until(() => _sync.Calls == 1);
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            feed.Send(": ping\n\n");
        }

        Assert.Equal(1, _stream.Opens);
        Assert.Empty(_waits);
    }
}
