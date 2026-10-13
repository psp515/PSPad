using System.Net;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class KeycloakTokenRevokerTests
{
    [Fact]
    public async Task ItPostsTheRefreshTokenToTheRealmsRevocationEndpoint()
    {
        var endpoint = new RecordingEndpoint(HttpStatusCode.OK);

        await Revoker(endpoint).RevokeAsync("refresh-token");

        Assert.Equal(HttpMethod.Post, endpoint.Method);
        Assert.Equal("http://localhost:8080/realms/psplace/protocol/openid-connect/revoke", endpoint.Uri);
        Assert.Equal(
            "client_id=pspad-frontend&token=refresh-token&token_type_hint=refresh_token", endpoint.Body);
    }

    [Fact]
    public async Task ASuccessIsARevocation()
    {
        Assert.Equal(
            RevocationOutcome.Revoked,
            await Revoker(new RecordingEndpoint(HttpStatusCode.OK)).RevokeAsync("refresh-token"));
    }

    [Fact]
    public async Task AClientErrorIsARefusalFromAReachableKeycloak()
    {
        Assert.Equal(
            RevocationOutcome.Refused,
            await Revoker(new RecordingEndpoint(HttpStatusCode.BadRequest)).RevokeAsync("refresh-token"));
    }

    [Fact]
    public async Task AServerErrorLeavesKeycloakUnreachable()
    {
        Assert.Equal(
            RevocationOutcome.Unreachable,
            await Revoker(new RecordingEndpoint(HttpStatusCode.ServiceUnavailable)).RevokeAsync("refresh-token"));
    }

    [Fact]
    public async Task ATransportFailureLeavesKeycloakUnreachable()
    {
        Assert.Equal(
            RevocationOutcome.Unreachable,
            await Revoker(new ThrowingEndpoint(new HttpRequestException("offline"))).RevokeAsync("refresh-token"));
    }

    [Fact]
    public async Task AStalledRequestLeavesKeycloakUnreachable()
    {
        Assert.Equal(
            RevocationOutcome.Unreachable,
            await Revoker(new ThrowingEndpoint(new TaskCanceledException())).RevokeAsync("refresh-token"));
    }

    static KeycloakTokenRevoker Revoker(HttpMessageHandler handler) =>
        new(new HttpClient(handler), "http://localhost:8080/realms/psplace/", "pspad-frontend");

    sealed class RecordingEndpoint(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Uri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    sealed class ThrowingEndpoint(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
