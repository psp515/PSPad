using PSPad.App.Push;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Push;

[UnitTest]
public class PushRefreshTests
{
    readonly FakePushDevice device = new();
    readonly FakePushApi api = new();
    readonly SwitchableConnectivity connectivity = new();

    PushRefresh Refresh => new(device, api, connectivity);

    [Fact]
    public async Task AnOnDeviceIsPutAgainWithItsCurrentKeys()
    {
        await Refresh.RefreshAsync();

        Assert.Equal([new PushSubscriptionRequest("https://push.example/a", "p256dh", "auth", "Linux")], api.Subscribed);
    }

    [Fact]
    public async Task NothingIsPutOffline()
    {
        connectivity.IsOnline = false;

        await Refresh.RefreshAsync();

        Assert.Empty(api.Subscribed);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("denied")]
    public async Task NothingIsPutWhenTheDeviceIsNotOn(string permission)
    {
        device.Support = device.Support with { Permission = permission };

        await Refresh.RefreshAsync();

        Assert.Empty(api.Subscribed);
    }

    [Fact]
    public async Task NothingIsPutWithoutASubscription()
    {
        device.Current = null;

        await Refresh.RefreshAsync();

        Assert.Empty(api.Subscribed);
    }

    [Fact]
    public async Task ABrowserFailureIsSwallowed()
    {
        device.Throws = true;

        await Refresh.RefreshAsync();

        Assert.Empty(api.Subscribed);
    }
}
