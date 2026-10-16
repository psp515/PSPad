using PSPad.App.Live;
using PSPad.App.Tests.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Live;

[UnitTest]
public class BrowserPageVisibilityTests
{
    [Fact]
    public void APageStartsVisible() =>
        Assert.True(new BrowserPageVisibility(new SilentJsRuntime()).IsVisible);

    [Fact]
    public void HidingAndShowingAnnounceTheChange()
    {
        var visibility = new BrowserPageVisibility(new SilentJsRuntime());
        var changes = 0;
        visibility.Changed += () => changes++;

        visibility.OnVisibilityChanged(false);
        Assert.False(visibility.IsVisible);

        visibility.OnVisibilityChanged(true);
        Assert.True(visibility.IsVisible);
        Assert.Equal(2, changes);
    }
}
