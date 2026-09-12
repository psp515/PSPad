using Microsoft.AspNetCore.Mvc.Testing;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
public class HealthEndpointTests
{
    [Fact]
    public async Task HealthRespondsWithoutAuthentication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", cancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
