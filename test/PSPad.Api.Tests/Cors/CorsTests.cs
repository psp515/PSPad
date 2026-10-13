using Microsoft.Extensions.Hosting;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Cors;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CorsTests(MongoFixture fixture)
{
    [Theory]
    [InlineData("https://pspad.psplace.dev")]
    [InlineData("https://staging.pspad.psplace.dev")]
    public async Task ProductionDefaultsAllowPsplaceSubdomains(string origin)
    {
        await using var factory = new ApiFactory(fixture, environment: Environments.Production);

        Assert.Equal(origin, await AllowedOriginAsync(factory, origin));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://pspad.psplace.dev")]
    [InlineData("https://pspad.psplace.dev.evil.example")]
    [InlineData("https://psplace.dev")]
    [InlineData("http://localhost:5001")]
    public async Task ProductionDefaultsRejectEveryOtherOrigin(string origin)
    {
        await using var factory = new ApiFactory(fixture, environment: Environments.Production);

        Assert.Null(await AllowedOriginAsync(factory, origin));
    }

    [Fact]
    public async Task DevelopmentAllowsTheLocalClientOnly()
    {
        await using var factory = new ApiFactory(fixture, environment: Environments.Development);

        Assert.Equal("http://localhost:5001", await AllowedOriginAsync(factory, "http://localhost:5001"));
        Assert.Null(await AllowedOriginAsync(factory, "https://pspad.psplace.dev"));
    }

    [Fact]
    public async Task AnExactOriginFromConfigurationReplacesTheDefault()
    {
        await using var factory = new ApiFactory(
            fixture,
            configuration: new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://pad.example.org" },
            environment: Environments.Production);

        Assert.Equal("https://pad.example.org", await AllowedOriginAsync(factory, "https://pad.example.org"));
        Assert.Null(await AllowedOriginAsync(factory, "https://pspad.psplace.dev"));
    }

    static async Task<string?> AllowedOriginAsync(ApiFactory factory, string origin)
    {
        var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/commands");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await client.SendAsync(preflight, TestContext.Current.CancellationToken);

        return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? values.Single()
            : null;
    }
}
