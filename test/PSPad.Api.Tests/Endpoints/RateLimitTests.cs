using System.Net;
using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class RateLimitTests(MongoFixture fixture)
{
    [Fact]
    public async Task CommandsBeyondTheUsersBucketAreRefusedWithRetryAfter()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, configuration: Bucket("Commands", 2));
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        await client.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);
        await client.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);
        var refused = await client.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task OneUsersExhaustedBucketLeavesAnotherUserAlone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, configuration: Bucket("Commands", 1));
        var noisy = factory.ClientFor(Guid.NewGuid().ToString());
        var quiet = factory.ClientFor(Guid.NewGuid().ToString());

        await noisy.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);
        var refused = await noisy.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);
        var served = await quiet.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task SyncPullsBeyondTheUsersBucketAreRefused()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, configuration: Bucket("Sync", 1));
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        var served = await client.GetAsync("/api/sync?since=0", ct);
        var refused = await client.GetAsync("/api/sync?since=0", ct);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task StatisticsHaveTheirOwnBucketApartFromCommands()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var configuration = Bucket("Commands", 1);
        foreach (var (key, value) in Bucket("Statistics", 2))
        {
            configuration[key] = value;
        }

        await using var factory = new ApiFactory(fixture, configuration: configuration);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        await client.PostAsJsonAsync("/api/commands", Array.Empty<CommandEnvelope>(), ct);
        var first = await client.GetAsync("/api/statistics/records", ct);
        var second = await client.GetAsync("/api/statistics/records", ct);
        var third = await client.GetAsync("/api/statistics/records", ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task EveryAuthenticatedApiCallDrawsOnTheUsersGeneralBucket()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, configuration: Bucket("Api", 1));
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        var served = await client.GetAsync("/api/me", ct);
        var refused = await client.GetAsync("/api/me", ct);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task TheHealthCheckIsNeverLimited()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, configuration: Bucket("Api", 1));
        var client = factory.CreateClient();

        await client.GetAsync("/health", ct);
        var second = await client.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    static Dictionary<string, string?> Bucket(string policy, int tokens) => new()
    {
        [$"RateLimits:{policy}:TokenLimit"] = tokens.ToString(),
        [$"RateLimits:{policy}:TokensPerPeriod"] = "1",
        [$"RateLimits:{policy}:ReplenishmentPeriod"] = "01:00:00"
    };
}
