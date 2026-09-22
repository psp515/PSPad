using System.Net;
using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class TokenRefresherTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ItRenewsFromASuccessfulExchange()
    {
        var refresher = Refresher(Respond(HttpStatusCode.OK,
            """{"access_token":"at","expires_in":300,"refresh_token":"rotated"}"""));

        var outcome = await refresher.RefreshAsync("stored");

        var renewed = Assert.IsType<RefreshOutcome.Renewed>(outcome);
        Assert.Equal("at", renewed.AccessToken);
        Assert.Equal("rotated", renewed.RefreshToken);
        Assert.Equal(Now.AddSeconds(300), renewed.ExpiresAt);
    }

    [Fact]
    public async Task ItTreatsInvalidGrantAsRevoked()
    {
        var refresher = Refresher(Respond(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"Token is not active"}"""));

        Assert.IsType<RefreshOutcome.Revoked>(await refresher.RefreshAsync("stored"));
    }

    [Fact]
    public async Task ItTreatsATransportFailureAsOfflineNotSignOut()
    {
        var refresher = Refresher(new ThrowingHandler(new HttpRequestException("offline")));

        Assert.IsType<RefreshOutcome.Offline>(await refresher.RefreshAsync("stored"));
    }

    [Fact]
    public async Task ItTreatsAServerErrorAsOffline()
    {
        var refresher = Refresher(Respond(HttpStatusCode.InternalServerError, ""));

        Assert.IsType<RefreshOutcome.Offline>(await refresher.RefreshAsync("stored"));
    }

    [Fact]
    public async Task ItCachesTheAccessTokenForTheHandler()
    {
        var refresher = Refresher(Respond(HttpStatusCode.OK,
            """{"access_token":"at","expires_in":300,"refresh_token":"rotated"}"""));

        await refresher.RefreshAsync("stored");

        Assert.Equal("at", refresher.AccessToken);
        Assert.Equal(Now.AddSeconds(300), refresher.AccessTokenExpiresAt);
    }

    static TokenRefresher Refresher(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");

    static StubHandler Respond(HttpStatusCode status, string body) => new(status, body);

    sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }

    sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
