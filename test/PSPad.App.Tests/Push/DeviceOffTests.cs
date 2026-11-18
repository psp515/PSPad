using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Push;

[UnitTest]
public class DeviceOffTests
{
    readonly FakePushDevice device = new();

    [Fact]
    public async Task AnOnDeviceIsNotOff() => Assert.False(await PushState.DeviceOffAsync(device));

    [Fact]
    public async Task ADeviceWithoutPermissionIsOff()
    {
        device.Support = device.Support with { Permission = "default" };

        Assert.True(await PushState.DeviceOffAsync(device));
    }

    [Fact]
    public async Task ABrowserFailureIsNotReportedAsOff()
    {
        device.Throws = true;

        Assert.False(await PushState.DeviceOffAsync(device));
    }
}
