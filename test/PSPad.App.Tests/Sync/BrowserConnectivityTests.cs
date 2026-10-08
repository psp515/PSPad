using Microsoft.JSInterop;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class BrowserConnectivityTests
{
    [Fact]
    public void GoingOfflineAnnouncesTheChange()
    {
        var connectivity = new BrowserConnectivity(new SilentJsRuntime(), new FakeProbe(true), TimeSpan.FromMilliseconds(10));
        var announcements = 0;
        connectivity.Changed += () => announcements++;

        connectivity.OnOffline();

        Assert.False(connectivity.IsOnline);
        Assert.Equal(1, announcements);
    }

    [Fact]
    public void ComingBackOnlineAnnouncesTheChangeAndTheLegacyEvent()
    {
        var connectivity = new BrowserConnectivity(new SilentJsRuntime(), new FakeProbe(true), TimeSpan.FromMilliseconds(10));
        connectivity.OnOffline();
        var changes = 0;
        var cameOnline = 0;
        connectivity.Changed += () => changes++;
        connectivity.CameOnline += () => cameOnline++;

        connectivity.OnOnline();

        Assert.True(connectivity.IsOnline);
        Assert.Equal(1, changes);
        Assert.Equal(1, cameOnline);
    }

    [Fact]
    public async Task ABrowserOfflineSignalTheServerContradictsIsIgnored()
    {
        var connectivity = new BrowserConnectivity(new SilentJsRuntime(), new FakeProbe(true), TimeSpan.FromMilliseconds(10));
        var changes = 0;
        connectivity.Changed += () => changes++;

        await connectivity.OnBrowserOffline();

        Assert.True(connectivity.IsOnline);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task ABrowserOfflineSignalTheServerConfirmsGoesOffline()
    {
        var connectivity = new BrowserConnectivity(new SilentJsRuntime(), new FakeProbe(false), TimeSpan.FromMilliseconds(10));

        await connectivity.OnBrowserOffline();

        Assert.False(connectivity.IsOnline);
    }

    [Fact]
    public async Task OfflineRecoversByItselfOnceTheServerAnswers()
    {
        var probe = new FakeProbe(false);
        var connectivity = new BrowserConnectivity(new SilentJsRuntime(), probe, TimeSpan.FromMilliseconds(10));
        var cameOnline = 0;
        connectivity.CameOnline += () => cameOnline++;
        await connectivity.OnBrowserOffline();

        probe.Reachable = true;

        await WaitUntilAsync(() => cameOnline == 1);
        Assert.Equal(1, cameOnline);
    }

    static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }

    sealed class FakeProbe(bool reachable) : IServerProbe
    {
        public bool Reachable { get; set; } = reachable;

        public Task<bool> ReachableAsync() => Task.FromResult(Reachable);
    }

    sealed class SilentJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            new(Task.FromException<TValue>(new InvalidOperationException("no interop in tests")));

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            new(Task.FromException<TValue>(new InvalidOperationException("no interop in tests")));
    }
}
