using System.Net.Http.Json;
using PSPad.Contracts;
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
}
