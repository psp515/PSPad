using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using PSPad.App.Auth;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Statistics;
using PSPad.App.Tests.State;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class LocalSignOutTests
{
    static readonly Guid User = Guid.NewGuid();

    readonly InMemoryLocalSessionStore _sessions = new(
        new LocalSession(User, "Ada", "ada@example.com", "UTC", "en", "refresh-token", DateTimeOffset.UtcNow, "id-token"));

    readonly InMemoryReplica _replica = new();

    readonly InMemoryOutbox _outbox = new();

    readonly StatisticsCache _statistics = new(new LocalStorage());

    readonly InMemorySnapshotCache _snapshots = new();

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
    public async Task ClearingRemovesTheStatisticsOverviewAndTheOpenedSnapshots()
    {
        await ArrangeStorageAsync();

        await SignOut(HttpStatusCode.OK).ClearAsync();

        Assert.Null(await _statistics.ReadAsync(30));
        Assert.Empty(await _snapshots.AllAsync());
    }

    [Fact]
    public async Task SigningOutLeavesNoStatisticsOrSnapshotsBehind()
    {
        await ArrangeStorageAsync();

        await SignOut(HttpStatusCode.OK).SignOutAsync();

        Assert.Null(await _statistics.ReadAsync(30));
        Assert.Empty(await _snapshots.AllAsync());
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
        Assert.NotNull(await _statistics.ReadAsync(30));
        Assert.NotEmpty(await _snapshots.AllAsync());
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
        await _statistics.WriteAsync(30, new StatisticsOverview(new StatisticsTilesView(0, 0, 0, 0), [], [], [], [], [], []));
        await _snapshots.SaveAsync(
            "snapshot-token",
            new SnapshotView(Guid.NewGuid(), "List", "Tasks", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), [], []),
            DateTimeOffset.UtcNow);
    }

    LocalSignOut SignOut(HttpStatusCode revocationStatus) =>
        SignOut(new RevocationEndpoint(revocationStatus, _sessions));

    LocalSignOut SignOut(RevocationEndpoint endpoint, InMemoryLocalSessionStore? sessions = null)
    {
        var store = sessions ?? _sessions;
        return new LocalSignOut(
            store, new LocalDeviceData(store, _replica, _outbox, _statistics, _snapshots, NullLogger<LocalDeviceData>.Instance),
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

    sealed class LocalStorage : IJSRuntime
    {
        readonly Dictionary<string, string> _values = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            var key = (string)args![0]!;

            switch (identifier)
            {
                case "localStorage.getItem":
                    return ValueTask.FromResult((TValue)(object?)(_values.TryGetValue(key, out var value) ? value : null)!);
                case "localStorage.setItem":
                    _values[key] = (string)args[1]!;
                    break;
                case "localStorage.removeItem":
                    _values.Remove(key);
                    break;
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
