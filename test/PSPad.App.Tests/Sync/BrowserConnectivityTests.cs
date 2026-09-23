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
        var connectivity = new BrowserConnectivity(new SilentJsRuntime());
        var announcements = 0;
        connectivity.Changed += () => announcements++;

        connectivity.OnOffline();

        Assert.False(connectivity.IsOnline);
        Assert.Equal(1, announcements);
    }

    [Fact]
    public void ComingBackOnlineAnnouncesTheChangeAndTheLegacyEvent()
    {
        var connectivity = new BrowserConnectivity(new SilentJsRuntime());
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

    sealed class SilentJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            new(Task.FromException<TValue>(new InvalidOperationException("no interop in tests")));

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            new(Task.FromException<TValue>(new InvalidOperationException("no interop in tests")));
    }
}
