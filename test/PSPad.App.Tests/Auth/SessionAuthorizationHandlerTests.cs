using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class SessionAuthorizationHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ItAttachesACachedTokenThatIsStillValid()
    {
        var refresher = await RefresherWithToken(expiresIn: 300);
        var captured = new CapturingHandler();
        var client = Client(refresher, new InMemoryLocalSessionStore(Session()), captured);

        await client.GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", captured.Request?.Headers.Authorization?.Scheme);
        Assert.Equal("at", captured.Request?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ItSendsUnauthenticatedWhenTheSessionStoreThrows()
    {
        var refresher = new TokenRefresher(
            new HttpClient(new ThrowingHandler()), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var captured = new CapturingHandler();
        var sessions = new ThrowingLocalSessionStore();
        var handler = new SessionAuthorizationHandler(
            refresher, sessions, new FixedClock(Now), new LocalAuthenticationStateProvider(sessions))
        {
            InnerHandler = captured
        };

        await new HttpClient(handler).GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Null(captured.Request?.Headers.Authorization);
    }

    [Fact]
    public async Task ItSendsUnauthenticatedWhenOfflineWithNoUsableToken()
    {
        var refresher = new TokenRefresher(
            new HttpClient(new ThrowingHandler()), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var captured = new CapturingHandler();
        var client = Client(refresher, new InMemoryLocalSessionStore(Session()), captured);

        await client.GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Null(captured.Request?.Headers.Authorization);
    }

    [Fact]
    public async Task ItRefreshesWhenTheCachedTokenIsInsideTheMargin()
    {
        var queue = new QueuingHandler(
            $$"""{"access_token":"first","expires_in":10,"refresh_token":"r1"}""",
            $$"""{"access_token":"second","expires_in":300,"refresh_token":"r2"}""");
        var refresher = new TokenRefresher(
            new HttpClient(queue), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        await refresher.RefreshAsync("stored");

        var captured = new CapturingHandler();
        var sessions = new InMemoryLocalSessionStore(Session());
        var client = Client(refresher, sessions, captured);

        await client.GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Equal("second", captured.Request?.Headers.Authorization?.Parameter);
        Assert.Equal("r2", sessions.Current?.RefreshToken);
        Assert.Equal(Now, sessions.Current?.LastServerContactUtc);
    }

    [Fact]
    public async Task ItReusesACachedTokenOutsideTheMarginWithoutRefreshing()
    {
        var queue = new QueuingHandler(
            $$"""{"access_token":"first","expires_in":300,"refresh_token":"r1"}""");
        var refresher = new TokenRefresher(
            new HttpClient(queue), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        await refresher.RefreshAsync("stored");

        var captured = new CapturingHandler();
        var client = Client(refresher, new InMemoryLocalSessionStore(Session()), captured);

        await client.GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Equal("first", captured.Request?.Headers.Authorization?.Parameter);
        Assert.Equal(1, queue.RequestCount);
    }

    [Fact]
    public async Task ItRefreshesOnceForOverlappingRequests()
    {
        var tokenResponse = new TaskCompletionSource<HttpResponseMessage>();
        var tokenEndpoint = new GatedHandler(tokenResponse.Task);
        var refresher = new TokenRefresher(
            new HttpClient(tokenEndpoint), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var captured = new MultiCapturingHandler();
        var client = Client(refresher, new InMemoryLocalSessionStore(Session()), captured);

        var first = client.GetAsync("http://api.test/me", TestContext.Current.CancellationToken);
        var second = client.GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        tokenResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"access_token":"shared","expires_in":300,"refresh_token":"rotated"}""")
        });

        await Task.WhenAll(first, second);

        Assert.Equal(1, tokenEndpoint.RequestCount);
        Assert.Equal(2, captured.Requests.Count);
        Assert.All(captured.Requests, r => Assert.Equal("shared", r.Headers.Authorization?.Parameter));
    }

    [Fact]
    public async Task ItSignsOutWhenTheAttemptedTokenWasStillCurrentAndRevoked()
    {
        var refresher = new TokenRefresher(
            new HttpClient(new RevokedHandler()), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var sessions = new InMemoryLocalSessionStore(Session());
        var authenticationState = new LocalAuthenticationStateProvider(sessions);
        var signOuts = 0;
        authenticationState.AuthenticationStateChanged += _ => signOuts++;
        var captured = new CapturingHandler();
        var handler = new SessionAuthorizationHandler(
            refresher, sessions, new FixedClock(Now), authenticationState)
        {
            InnerHandler = captured
        };

        await new HttpClient(handler).GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Null(captured.Request?.Headers.Authorization);
        Assert.Null(sessions.Current);
        Assert.Equal(1, sessions.Clears);
        Assert.Equal(1, signOuts);
    }

    [Fact]
    public async Task ItStaysOfflineWithoutSigningOutWhenAnotherContextAlreadyRotatedTheToken()
    {
        var refresher = new TokenRefresher(
            new HttpClient(new RevokedHandler()), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var attempted = Session();
        var afterRotation = attempted with { RefreshToken = "rotated-elsewhere" };
        var sessions = new RotatingLocalSessionStore(attempted, afterRotation);
        var authenticationState = new LocalAuthenticationStateProvider(sessions);
        var signOuts = 0;
        authenticationState.AuthenticationStateChanged += _ => signOuts++;
        var captured = new CapturingHandler();
        var handler = new SessionAuthorizationHandler(
            refresher, sessions, new FixedClock(Now), authenticationState)
        {
            InnerHandler = captured
        };

        await new HttpClient(handler).GetAsync("http://api.test/me", TestContext.Current.CancellationToken);

        Assert.Null(captured.Request?.Headers.Authorization);
        Assert.Equal(0, sessions.ClearCalls);
        Assert.Equal(0, signOuts);
    }

    static async Task<TokenRefresher> RefresherWithToken(int expiresIn)
    {
        var refresher = new TokenRefresher(
            new HttpClient(new StubHandler(
                $$"""{"access_token":"at","expires_in":{{expiresIn}},"refresh_token":"rotated"}""")),
            new FixedClock(Now), "http://localhost:8080/realms/psplace", "pspad-frontend");

        await refresher.RefreshAsync("stored");

        return refresher;
    }

    static HttpClient Client(
        TokenRefresher refresher, InMemoryLocalSessionStore sessions, HttpMessageHandler inner)
    {
        var handler = new SessionAuthorizationHandler(
            refresher, sessions, new FixedClock(Now), new LocalAuthenticationStateProvider(sessions))
        {
            InnerHandler = inner
        };

        return new HttpClient(handler);
    }

    static LocalSession Session() =>
        new(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "refresh", Now);

    sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
    }

    sealed class QueuingHandler(params string[] bodies) : HttpMessageHandler
    {
        readonly Queue<string> bodies = new(bodies);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(bodies.Dequeue())
            });
        }
    }

    sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    sealed class RevokedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_grant"}""")
            });
    }

    sealed class GatedHandler(Task<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return response;
        }
    }

    sealed class MultiCapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    sealed class RotatingLocalSessionStore(LocalSession first, LocalSession afterRotation)
        : ILocalSessionStore
    {
        int loads;

        public int ClearCalls { get; private set; }

        public Task<LocalSession?> LoadAsync()
        {
            loads++;
            return Task.FromResult<LocalSession?>(loads == 1 ? first : afterRotation);
        }

        public Task SaveAsync(LocalSession session) => Task.CompletedTask;

        public Task ClearAsync()
        {
            ClearCalls++;
            return Task.CompletedTask;
        }
    }

    sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
