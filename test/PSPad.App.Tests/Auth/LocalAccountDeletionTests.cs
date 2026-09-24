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
}
