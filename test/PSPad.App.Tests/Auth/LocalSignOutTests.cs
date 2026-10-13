using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PSPad.App.Auth;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class LocalSignOutTests
{
    static readonly Guid User = Guid.NewGuid();

    readonly InMemoryLocalSessionStore _sessions = new(
        new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow, "id-token"));

    readonly InMemoryReplica _replica = new();

    readonly InMemoryOutbox _outbox = new();

    [Fact]
    public async Task ClearingRemovesTheOutboxWithTheReplicaAndTheSession()
    {
        await ArrangeStorageAsync();

        await SignOut(HttpStatusCode.OK).ClearAsync();

        Assert.Null(_sessions.Current);
        Assert.Null(await _replica.OwnerAsync());
        Assert.Equal(0, await _outbox.CountAsync());
    }

    [Fact]
    public async Task SigningOutRevokesTheStoredRefreshTokenBeforeClearingAnything()
    {
        await ArrangeStorageAsync();
        var endpoint = new RevocationEndpoint(HttpStatusCode.OK, _sessions);

        await SignOut(endpoint).SignOutAsync();

        Assert.Equal("refresh-token", endpoint.RevokedToken);
        Assert.True(endpoint.SessionWasStillStored);
    }

    [Fact]
    public async Task SigningOutClearsEverythingAndHandsBackTheIdTokenAsTheEndSessionHint()
    {
        await ArrangeStorageAsync();

        var outcome = await SignOut(HttpStatusCode.OK).SignOutAsync();

        var cleared = Assert.IsType<SignOutOutcome.Cleared>(outcome);
        Assert.Equal("id-token", cleared.IdTokenHint);
        Assert.Null(_sessions.Current);
        Assert.Null(await _replica.OwnerAsync());
        Assert.Equal(0, await _outbox.CountAsync());
    }

    [Fact]
    public async Task AnUnreachableKeycloakLeavesTheUserSignedInWithEverythingKept()
    {
        await ArrangeStorageAsync();

        var outcome = await SignOut(HttpStatusCode.ServiceUnavailable).SignOutAsync();

        Assert.IsType<SignOutOutcome.KeycloakUnreachable>(outcome);
        Assert.NotNull(_sessions.Current);
        Assert.Equal(User, await _replica.OwnerAsync());
        Assert.Equal(1, await _outbox.CountAsync());
    }

    [Fact]
    public async Task ARefusedRevocationStillSignsOutBecauseEndSessionKillsTheTokenAnyway()
    {
        await ArrangeStorageAsync();

        var outcome = await SignOut(HttpStatusCode.BadRequest).SignOutAsync();

        Assert.IsType<SignOutOutcome.Cleared>(outcome);
        Assert.Null(_sessions.Current);
    }

    [Fact]
    public async Task WithNoStoredSessionThereIsNothingToRevokeAndNoHint()
    {
        var sessions = new InMemoryLocalSessionStore();
        var endpoint = new RevocationEndpoint(HttpStatusCode.OK, sessions);

        var outcome = await SignOut(endpoint, sessions).SignOutAsync();

        var cleared = Assert.IsType<SignOutOutcome.Cleared>(outcome);
        Assert.Null(cleared.IdTokenHint);
        Assert.Null(endpoint.RevokedToken);
    }

    async Task ArrangeStorageAsync()
    {
        await _replica.SetOwnerAsync(User);
        await _outbox.AppendAsync(
            Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
    }

    LocalSignOut SignOut(HttpStatusCode revocationStatus) =>
        SignOut(new RevocationEndpoint(revocationStatus, _sessions));

    LocalSignOut SignOut(RevocationEndpoint endpoint, InMemoryLocalSessionStore? sessions = null)
    {
        var store = sessions ?? _sessions;
        return new LocalSignOut(
            store, _replica, _outbox,
            new KeycloakTokenRevoker(new HttpClient(endpoint), "http://localhost:8080/realms/psplace", "pspad-frontend"),
            new LocalAuthenticationStateProvider(store), NullLogger<LocalSignOut>.Instance);
    }

    sealed class RevocationEndpoint(HttpStatusCode status, InMemoryLocalSessionStore sessions) : HttpMessageHandler
    {
        public string? RevokedToken { get; private set; }

        public bool SessionWasStillStored { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            RevokedToken = form.Split('&').Single(pair => pair.StartsWith("token=")).Split('=')[1];
            SessionWasStillStored = sessions.Current is not null;
            return new HttpResponseMessage(status);
        }
    }
}
