using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Api.Reminders;
using PSPad.Api.Tests.Reminders;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Reminders;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class PushEndpointTests(MongoFixture fixture)
{
    static CancellationToken Ct => global::Xunit.TestContext.Current.CancellationToken;

    ApiFactory FactoryWith(FakePushService push) =>
        new(fixture, null,
            new Dictionary<string, string?> { ["Reminders:AppBaseAddress"] = "https://pspad.test" },
            services => services.AddHttpClient<IPushSender, WebPushSender>()
                .ConfigurePrimaryHttpMessageHandler(() => push));

    static async Task<HttpClient> UserAsync(ApiFactory factory)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(client, Ct);
        return client;
    }

    static Task<HttpResponseMessage> Subscribe(HttpClient client, string endpoint, string p256dh = "AQID", string auth = "BAUG") =>
        client.PutAsJsonAsync("/api/push/subscriptions", new PushSubscriptionRequest(endpoint, p256dh, auth, "Phone"), Ct);

    static async Task<PushSubscriptionRecord?> Stored(ApiFactory factory, string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>().FindAsync(endpoint, Ct);
    }

    static Task<HttpResponseMessage> Delete(HttpClient client, string endpoint) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/push/subscriptions")
        {
            Content = JsonContent.Create(new PushEndpointRequest(endpoint)),
        }, Ct);

    [Fact]
    public async Task TheKeyIsTheStoredPublicKey()
    {
        await using var factory = FactoryWith(new FakePushService());
        var client = await UserAsync(factory);

        var first = await client.GetFromJsonAsync<PushKeyResponse>("/api/push/key", Ct);
        var second = await client.GetFromJsonAsync<PushKeyResponse>("/api/push/key", Ct);

        var keys = await factory.Services.GetRequiredService<VapidKeyring>().GetAsync(Ct);
        Assert.Equal(keys.PublicKey, first!.PublicKey);
        Assert.Equal(first.PublicKey, second!.PublicKey);
    }

    [Fact]
    public async Task ASubscriptionIsSavedAndReplacedByEndpoint()
    {
        await using var factory = FactoryWith(new FakePushService());
        var client = await UserAsync(factory);
        var endpoint = $"https://push.test/{Guid.NewGuid()}";

        Assert.Equal(HttpStatusCode.NoContent, (await Subscribe(client, endpoint)).StatusCode);
        var first = await Stored(factory, endpoint);
        await Task.Delay(20, Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await Subscribe(client, endpoint, "CQoL", "DA0O")).StatusCode);
        var second = await Stored(factory, endpoint);

        Assert.Equal("CQoL", second!.P256dh);
        Assert.Equal("DA0O", second.Auth);
        Assert.Equal(first!.CreatedAt, second.CreatedAt);
    }

    [Fact]
    public async Task AnEndpointMovesToWhoeverSubscribesIt()
    {
        await using var factory = FactoryWith(new FakePushService());
        var a = await UserAsync(factory);
        var b = await UserAsync(factory);
        var bId = (await b.GetFromJsonAsync<MeResponse>("/api/me", Ct))!.UserId;
        var endpoint = $"https://push.test/{Guid.NewGuid()}";

        await Subscribe(a, endpoint);
        await Subscribe(b, endpoint);

        Assert.Equal(bId, (await Stored(factory, endpoint))!.UserId);
    }

    [Fact]
    public async Task AUserCannotDeleteAnotherUsersSubscription()
    {
        await using var factory = FactoryWith(new FakePushService());
        var owner = await UserAsync(factory);
        var other = await UserAsync(factory);
        var endpoint = $"https://push.test/{Guid.NewGuid()}";
        await Subscribe(owner, endpoint);

        var response = await Delete(other, endpoint);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(await Stored(factory, endpoint));
    }

    [Fact]
    public async Task DeletingYourOwnSubscriptionRemovesIt()
    {
        await using var factory = FactoryWith(new FakePushService());
        var client = await UserAsync(factory);
        var endpoint = $"https://push.test/{Guid.NewGuid()}";
        await Subscribe(client, endpoint);

        var response = await Delete(client, endpoint);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await Stored(factory, endpoint));
    }

    [Fact]
    public async Task ATestPushGoesOnlyToTheCallersDevice()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var client = await UserAsync(factory);
        var stranger = await UserAsync(factory);
        using var device = BrowserDevice.Create();
        await Subscribe(client, device.Endpoint, Base64Url.Encode(device.PublicKey), Base64Url.Encode(device.Auth));

        var response = await client.PostAsJsonAsync("/api/push/test", new PushEndpointRequest(device.Endpoint), Ct);
        var refused = await stranger.PostAsJsonAsync("/api/push/test", new PushEndpointRequest(device.Endpoint), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        var request = Assert.Single(push.To(device));
        using var payload = JsonDocument.Parse(device.Decrypt(request));
        Assert.Equal("PSPad notifications work", payload.RootElement.GetProperty("title").GetString());
        Assert.Single(push.Requests);
    }

    [Theory]
    [InlineData("ftp://x", "AQID", "BAUG")]
    [InlineData("https://push.test/a", "", "BAUG")]
    [InlineData("https://push.test/a", "AQID", "")]
    [InlineData("LONG", "AQID", "BAUG")]
    [InlineData("http://push.example/x", "AQID", "BAUG")]
    [InlineData("http://localhost/x", "AQID", "BAUG")]
    [InlineData("https://10.0.0.1/x", "AQID", "BAUG")]
    [InlineData("https://[::1]/x", "AQID", "BAUG")]
    [InlineData("https://push.test/a", "AQ+D", "BAUG")]
    [InlineData("https://push.test/a", "AQID", "BA/G")]
    public async Task ABadSubscriptionIsRefused(string endpoint, string p256dh, string auth)
    {
        await using var factory = FactoryWith(new FakePushService());
        var client = await UserAsync(factory);
        var value = endpoint == "LONG" ? "https://push.test/" + new string('a', 3000) : endpoint;

        var response = await Subscribe(client, value, p256dh, auth);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ALongLabelIsTrimmed()
    {
        await using var factory = FactoryWith(new FakePushService());
        var client = await UserAsync(factory);
        var endpoint = $"https://push.test/{Guid.NewGuid()}";

        await client.PutAsJsonAsync("/api/push/subscriptions",
            new PushSubscriptionRequest(endpoint, "AQID", "BAUG", "  " + new string('x', 150)), Ct);

        Assert.Equal(100, (await Stored(factory, endpoint))!.DeviceLabel.Length);
    }

    [Fact]
    public async Task AMovedEndpointStartsFresh()
    {
        await using var factory = FactoryWith(new FakePushService());
        var a = await UserAsync(factory);
        var b = await UserAsync(factory);
        var endpoint = $"https://push.test/{Guid.NewGuid()}";
        await Subscribe(a, endpoint);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>()
                .MarkDeliveredAsync(endpoint, DateTimeOffset.UtcNow, Ct);
        }

        Assert.NotNull((await Stored(factory, endpoint))!.LastSuccessAt);
        await Task.Delay(20, Ct);
        await Subscribe(b, endpoint);

        Assert.Null((await Stored(factory, endpoint))!.LastSuccessAt);
    }

    [Fact]
    public async Task AMissingEndpointIsRefused()
    {
        await using var factory = FactoryWith(new FakePushService());
        var client = await UserAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await Delete(client, "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/push/test", new PushEndpointRequest(""), Ct)).StatusCode);
    }

    [Fact]
    public async Task TheEndpointsNeedASignedInUser()
    {
        await using var factory = FactoryWith(new FakePushService());
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/push/key", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Subscribe(anonymous, "https://push.test/a")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Delete(anonymous, "https://push.test/a")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/push/test", new PushEndpointRequest("https://push.test/a"), Ct)).StatusCode);
    }
}
