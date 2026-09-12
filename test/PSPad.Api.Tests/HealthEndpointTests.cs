using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class HealthEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task HealthRespondsWithoutAuthentication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
