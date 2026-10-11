using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class RequestBodyLimitTests(MongoFixture fixture)
{
    [Fact]
    public void KestrelAcceptsAtMostOneMegabyteOfRequestBody()
    {
        using var factory = new ApiFactory(fixture);

        var kestrel = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.Equal(1024 * 1024, kestrel.Limits.MaxRequestBodySize);
    }
}
