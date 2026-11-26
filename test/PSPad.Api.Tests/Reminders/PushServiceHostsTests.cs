using PSPad.Api.Reminders;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Reminders;

[UnitTest]
public class PushServiceHostsTests
{
    static readonly PushServiceHosts BuiltIn = new("");

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://android.googleapis.com/gcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
    [InlineData("https://web.push.apple.com/QGuQ")]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc")]
    [InlineData("https://FCM.GOOGLEAPIS.COM/fcm/send/abc")]
    public void TheBrowserPushServicesAreKnown(string endpoint) =>
        Assert.True(BuiltIn.IsKnown(new Uri(endpoint)));

    [Theory]
    [InlineData("https://evil.example/fcm/send/abc")]
    [InlineData("https://fcm.googleapis.com.evil.example/abc")]
    [InlineData("https://googleapis.com/abc")]
    [InlineData("https://storage.googleapis.com/abc")]
    [InlineData("https://push.apple.com.evil.example/abc")]
    [InlineData("https://xnotify.windows.com/abc")]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc")]
    public void AnyOtherAddressIsUnknown(string endpoint) =>
        Assert.False(BuiltIn.IsKnown(new Uri(endpoint)));

    [Fact]
    public void AnExtraHostIsKnownExactly()
    {
        var hosts = new PushServiceHosts("push.example.org");

        Assert.True(hosts.IsKnown(new Uri("https://push.example.org/abc")));
        Assert.False(hosts.IsKnown(new Uri("https://eu.push.example.org/abc")));
    }

    [Fact]
    public void AnExtraHostWithALeadingDotCoversItsSubdomains()
    {
        var hosts = new PushServiceHosts(" .push.example.org , other.example ");

        Assert.True(hosts.IsKnown(new Uri("https://eu.push.example.org/abc")));
        Assert.False(hosts.IsKnown(new Uri("https://push.example.org/abc")));
        Assert.True(hosts.IsKnown(new Uri("https://other.example/abc")));
    }
}
