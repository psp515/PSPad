using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Proxy;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ProxyStartupTests(MongoFixture fixture)
{
    [Fact]
    public async Task TheApiRefusesToStartWhenForwardedHeadersAreTrustedFromAnyone()
    {
        await using var factory = new ApiFactory(
            fixture, configuration: new Dictionary<string, string?> { ["ForwardedHeaders_Enabled"] = "true" });

        var failure = Record.Exception(() => factory.CreateClient());

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("Proxy__KnownNetworks__0", failure.Message);
    }
}
