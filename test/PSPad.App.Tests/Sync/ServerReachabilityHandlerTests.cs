using System.Net;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class ServerReachabilityHandlerTests
{
    [Fact]
    public async Task ATransportFailureWhileOnlineMarksTheServerUnreachable()
    {
        var reachability = new ServerReachability();
        var client = Client(reachability, new SpyConnectivity(isOnline: true), new ThrowingHandler());

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/api/me", TestContext.Current.CancellationToken));

        Assert.False(reachability.IsReachable);
    }

    [Fact]
    public async Task ATransportFailureWhileOfflineLeavesTheServerReachable()
    {
        var reachability = new ServerReachability();
        var client = Client(reachability, new SpyConnectivity(isOnline: false), new ThrowingHandler());

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/api/me", TestContext.Current.CancellationToken));

        Assert.True(reachability.IsReachable);
    }

    [Fact]
    public async Task AnAnsweredRequestRestoresReachabilityEvenWhenTheAnswerIsAnError()
    {
        var reachability = new ServerReachability();
        reachability.Failed(browserIsOnline: true);
        var client = Client(
            reachability,
            new SpyConnectivity(isOnline: true),
            new RespondingHandler(HttpStatusCode.Unauthorized));

        await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.True(reachability.IsReachable);
    }

    static HttpClient Client(ServerReachability reachability, IConnectivity connectivity, HttpMessageHandler inner)
    {
        var handler = new ServerReachabilityHandler(reachability, connectivity) { InnerHandler = inner };

        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }

    sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("TypeError: Failed to fetch");
    }

    sealed class RespondingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    sealed class SpyConnectivity(bool isOnline) : IConnectivity
    {
        public bool IsOnline { get; } = isOnline;

        public event Action? CameOnline
        {
            add { }
            remove { }
        }

        public event Action? Changed
        {
            add { }
            remove { }
        }
    }
}
