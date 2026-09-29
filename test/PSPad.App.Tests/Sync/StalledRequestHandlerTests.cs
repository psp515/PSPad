using System.Net;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class StalledRequestHandlerTests
{
    [Fact]
    public async Task ARequestThatNeverAnswersFailsAsATransportFailure()
    {
        var client = Client(TimeSpan.FromMilliseconds(20), new HangingHandler());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("/api/me", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARequestThatAnswersInTimePassesThrough()
    {
        var client = Client(TimeSpan.FromSeconds(5), new RespondingHandler());

        var response = await client.GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ACallerCancellationStaysACancellation()
    {
        var client = Client(TimeSpan.FromSeconds(5), new HangingHandler());
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync("/api/me", caller.Token));
    }

    [Fact]
    public async Task AStalledRequestMarksTheServerUnreachable()
    {
        var reachability = new ServerReachability();
        var stalled = new StalledRequestHandler(TimeSpan.FromMilliseconds(20)) { InnerHandler = new HangingHandler() };
        var handler = new ServerReachabilityHandler(reachability, new Online()) { InnerHandler = stalled };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("/api/me", TestContext.Current.CancellationToken));

        Assert.False(reachability.IsReachable);
    }

    static HttpClient Client(TimeSpan limit, HttpMessageHandler inner) =>
        new(new StalledRequestHandler(limit) { InnerHandler = inner }) { BaseAddress = new Uri("http://localhost/") };

    sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    sealed class RespondingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    sealed class Online : IConnectivity
    {
        public bool IsOnline => true;

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
