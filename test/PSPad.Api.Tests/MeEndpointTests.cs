using System.Net;
using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MeEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task ItReturnsTheTokenNameRatherThanTheSubject()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var subject = Guid.NewGuid().ToString();
        var client = factory.ClientFor(subject, name: "Ada Lovelace", email: "ada@example.com");

        var me = await client.GetFromJsonAsync<MeResponse>("api/me", ct);

        Assert.NotNull(me);
        Assert.Equal("Ada Lovelace", me.DisplayName);
    }

    [Fact]
    public async Task ItReturnsTheTokenEmail()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var subject = Guid.NewGuid().ToString();
        var client = factory.ClientFor(subject, name: "Ada Lovelace", email: "ada@example.com");

        var me = await client.GetFromJsonAsync<MeResponse>("api/me", ct);

        Assert.NotNull(me);
        Assert.Equal("ada@example.com", me.Email);
    }

    [Fact]
    public async Task ItHealsADisplayNameStoredBeforeTheClaimFixLanded()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var subject = Guid.NewGuid().ToString();

        var stale = factory.ClientFor(subject, name: subject, email: "ada@example.com");
        await stale.GetFromJsonAsync<MeResponse>("api/me", ct);

        var current = factory.ClientFor(subject, name: "Ada Lovelace", email: "ada@example.com");
        var me = await current.GetFromJsonAsync<MeResponse>("api/me", ct);

        Assert.NotNull(me);
        Assert.Equal("Ada Lovelace", me.DisplayName);
    }

    [Fact]
    public async Task ItSetsTheTimeZone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(
            Guid.NewGuid().ToString(), name: "Ada Lovelace", email: "ada@example.com");

        var response = await client.PutAsJsonAsync(
            "api/me/timezone", new SetTimeZoneRequest("Europe/Warsaw"), ct);

        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);
        Assert.Equal("Europe/Warsaw", me!.TimeZone);
    }

    [Fact]
    public async Task ItRejectsAZoneTheServerDoesNotKnow()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(
            Guid.NewGuid().ToString(), name: "Ada Lovelace", email: "ada@example.com");

        var response = await client.PutAsJsonAsync(
            "api/me/timezone", new SetTimeZoneRequest("Mars/Olympus_Mons"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
