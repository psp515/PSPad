using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BrowserTimeZoneProvisioningTests(MongoFixture fixture)
{
    static CancellationToken Ct => global::Xunit.TestContext.Current.CancellationToken;

    static HttpClient Without(ApiFactory factory, string? tokenZone, string? browserZone)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Remove("X-Test-Zone");
        if (tokenZone is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Zone", tokenZone);
        }

        if (browserZone is not null)
        {
            client.DefaultRequestHeaders.Add(TimeZoneHint.Header, browserZone);
        }

        return client;
    }

    [Fact]
    public async Task ANewAccountTakesTheBrowsersZoneWhenTheTokenHasNone()
    {
        await using var factory = new ApiFactory(fixture);

        var me = await Without(factory, null, "Europe/Warsaw").GetFromJsonAsync<MeResponse>("/api/me", Ct);

        Assert.Equal("Europe/Warsaw", me!.TimeZone);
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Etc/GMT")]
    public async Task ABrowserOnUtcIsStoredAsEtcUtc(string browserZone)
    {
        await using var factory = new ApiFactory(fixture);

        var me = await Without(factory, null, browserZone).GetFromJsonAsync<MeResponse>("/api/me", Ct);

        Assert.Equal("Etc/UTC", me!.TimeZone);
    }

    [Fact]
    public async Task AnUnknownBrowserZoneFallsBackToUtc()
    {
        await using var factory = new ApiFactory(fixture);

        var me = await Without(factory, null, "Mars/Olympus").GetFromJsonAsync<MeResponse>("/api/me", Ct);

        Assert.Equal("Etc/UTC", me!.TimeZone);
    }

    [Fact]
    public async Task TheTokensZoneWinsOverTheBrowsers()
    {
        await using var factory = new ApiFactory(fixture);

        var me = await Without(factory, "Asia/Tokyo", "Europe/Warsaw").GetFromJsonAsync<MeResponse>("/api/me", Ct);

        Assert.Equal("Asia/Tokyo", me!.TimeZone);
    }

    [Fact]
    public async Task AnExistingAccountKeepsItsZoneWhateverTheBrowserSays()
    {
        await using var factory = new ApiFactory(fixture);
        var subject = Guid.NewGuid().ToString();
        var first = factory.ClientFor(subject);
        first.DefaultRequestHeaders.Remove("X-Test-Zone");
        await first.GetFromJsonAsync<MeResponse>("/api/me", Ct);

        var later = factory.ClientFor(subject);
        later.DefaultRequestHeaders.Remove("X-Test-Zone");
        later.DefaultRequestHeaders.Add(TimeZoneHint.Header, "Europe/Warsaw");
        var me = await later.GetFromJsonAsync<MeResponse>("/api/me", Ct);

        Assert.Equal("Etc/UTC", me!.TimeZone);
    }
}
