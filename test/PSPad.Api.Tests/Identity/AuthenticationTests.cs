using System.Net;
using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class AuthenticationTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnUnauthenticatedCallIsRefused()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);

        var response = await factory.CreateClient().GetAsync("/api/today", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthStaysOpen()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);

        var response = await factory.CreateClient().GetAsync("/health", ct);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MeProvisionsOnTheFirstCallAndIsStableAfterwards()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(subject, "Europe/Warsaw");

        var first = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var second = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);

        Assert.Equal(first!.UserId, second!.UserId);
        Assert.Equal("Europe/Warsaw", first.TimeZone);
    }

    [Fact]
    public void TheHeaderSeamIsGone()
    {
        var files = Directory.EnumerateFiles(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"),
            "*.cs", SearchOption.AllDirectories);

        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains("X-User-Id"));
    }
}
