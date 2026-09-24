using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.Contracts;
using PSPad.TestInfrastructure;
using System.Text.Json;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class LocalAccountDeletionTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public async Task ClearAsyncWipesTheReplicaTheOutboxAndTheSession()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow));
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(User);
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        var deletion = new LocalAccountDeletion(sessions, replica, outbox, authProvider);

        await deletion.ClearAsync();

        Assert.Null(sessions.Current);
        Assert.Null(await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task ClearAsyncAnnouncesSignedOut()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow));
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        var deletion = new LocalAccountDeletion(sessions, replica, outbox, authProvider);
        var announced = false;
        authProvider.AuthenticationStateChanged += _ => announced = true;

        await deletion.ClearAsync();

        Assert.True(announced);
    }

    [Fact]
    public async Task AThrowingReplicaStillLeadsToSignedOutBeingAnnounced()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow));
        var outbox = new InMemoryOutbox();
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        var deletion = new LocalAccountDeletion(sessions, new ThrowingReplica(), outbox, authProvider);
        var announced = false;
        authProvider.AuthenticationStateChanged += _ => announced = true;

        await deletion.ClearAsync();

        Assert.True(announced);
        Assert.Null(sessions.Current);
    }

    [Fact]
    public async Task AThrowingOutboxStillLeadsToSignedOutBeingAnnounced()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow));
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(User);
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        var deletion = new LocalAccountDeletion(sessions, replica, new ThrowingOutbox(), authProvider);
        var announced = false;
        authProvider.AuthenticationStateChanged += _ => announced = true;

        await deletion.ClearAsync();

        Assert.True(announced);
        Assert.Null(sessions.Current);
        Assert.Null(await replica.OwnerAsync());
    }

    [Fact]
    public async Task AThrowingSessionStoreStillLeadsToSignedOutBeingAnnounced()
    {
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(User);
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
        var throwingSessions = new ThrowingLocalSessionStore();
        var authProvider = new LocalAuthenticationStateProvider(throwingSessions);
        var deletion = new LocalAccountDeletion(throwingSessions, replica, outbox, authProvider);
        var announced = false;
        authProvider.AuthenticationStateChanged += _ => announced = true;

        await deletion.ClearAsync();

        Assert.True(announced);
        Assert.Null(await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
    }

    sealed class ThrowingReplica : IReplica
    {
        public Task<T?> LoadAsync<T>(Guid id) where T : Aggregate => throw new NotSupportedException();

        public Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate =>
            throw new NotSupportedException();

        public Task SaveAsync(Aggregate aggregate) => throw new NotSupportedException();

        public Task<long> MarkerAsync() => throw new NotSupportedException();

        public Task SetMarkerAsync(long marker) => throw new NotSupportedException();

        public Task<Guid?> OwnerAsync() => Task.FromResult<Guid?>(null);

        public Task SetOwnerAsync(Guid userId) => Task.CompletedTask;

        public Task ClearAsync() => throw new InvalidOperationException("Replica clear boom.");
    }

    sealed class ThrowingOutbox : IOutbox
    {
        public Task AppendAsync(Guid commandId, CommandEnvelope envelope) => Task.CompletedTask;

        public Task<IReadOnlyList<OutboxEntry>> PeekAsync(int limit) =>
            Task.FromResult<IReadOnlyList<OutboxEntry>>([]);

        public Task RemoveThroughAsync(long position) => Task.CompletedTask;

        public Task<int> CountAsync() => Task.FromResult(0);

        public Task ClearAsync() => throw new InvalidOperationException("Outbox clear boom.");
    }

    sealed class ThrowingLocalSessionStore : ILocalSessionStore
    {
        public Task<LocalSession?> LoadAsync() => Task.FromResult<LocalSession?>(null);

        public Task SaveAsync(LocalSession session) => Task.CompletedTask;

        public Task ClearAsync() => throw new InvalidOperationException("Session clear boom.");
    }
}
