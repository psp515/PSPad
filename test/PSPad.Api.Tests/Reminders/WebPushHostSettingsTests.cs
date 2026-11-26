using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PSPad.Api.Reminders;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Reminders;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class WebPushHostSettingsTests(MongoFixture fixture)
{
    [Fact]
    public async Task TheBrowserPushServicesShipAsDefaultsInSettings()
    {
        await using var factory = new ApiFactory(fixture);

        var options = factory.Services.GetRequiredService<IOptions<ReminderOptions>>().Value;

        Assert.Equal(
            ["fcm.googleapis.com", "android.googleapis.com", ".push.services.mozilla.com", ".push.apple.com", ".notify.windows.com"],
            options.WebPushHosts);
    }

    [Fact]
    public async Task AdditionalWebPushHostsAreKnownBesideTheDefaults()
    {
        await using var factory = new ApiFactory(fixture);

        var hosts = factory.Services.GetRequiredService<PushServiceHosts>();

        Assert.True(hosts.IsKnown(new Uri("https://push.test/abc")));
        Assert.True(hosts.IsKnown(new Uri("https://fcm.googleapis.com/fcm/send/abc")));
        Assert.False(hosts.IsKnown(new Uri("https://evil.example/abc")));
    }
}
