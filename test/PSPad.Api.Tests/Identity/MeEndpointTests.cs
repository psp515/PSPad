using System.Net;
using System.Net.Http.Json;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MeEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task ItReportsTheNameClaimAsTheDisplayName()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Test-Subject", subject);
        request.Headers.Add("X-Test-Name", "Demo User");

        var response = await client.SendAsync(request, ct);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);

        Assert.Equal("Demo User", me!.DisplayName);
    }

    [Fact]
    public async Task WithoutANameClaimItFallsBackToPreferredUsername()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Test-Subject", subject);
        request.Headers.Add("X-Test-Preferred-Username", "demo");

        var response = await client.SendAsync(request, ct);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);

        Assert.Equal("demo", me!.DisplayName);
    }

    [Fact]
    public async Task WithNeitherClaimItFallsBackToTheSubject()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Test-Subject", subject);

        var response = await client.SendAsync(request, ct);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);

        Assert.Equal(subject, me!.DisplayName);
    }

    [Fact]
    public async Task ItDoesNotOverwriteAHealedNameWhenTheTokenFallsBackToTheSubject()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var healed = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        healed.Headers.Add("X-Test-Subject", subject);
        healed.Headers.Add("X-Test-Name", "Ada Lovelace");
        await client.SendAsync(healed, ct);

        var fallback = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        fallback.Headers.Add("X-Test-Subject", subject);

        var response = await client.SendAsync(fallback, ct);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(ct);

        Assert.Equal("Ada Lovelace", me!.DisplayName);

        var renames = await Persistence.TestContext.For(fixture)
            .Collection<StoredEvent>("events")
            .CountDocumentsAsync(
                Builders<StoredEvent>.Filter.Eq(stored => stored.UserId, me.UserId) &
                Builders<StoredEvent>.Filter.Eq(stored => stored.Type, nameof(UserDisplayNameSet)),
                cancellationToken: ct);

        Assert.Equal(0, renames);
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
