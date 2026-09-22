using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.App.State.Replica;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class SessionBootstrapperTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ItReportsNoSessionWhenNoneWasStored()
    {
        var sessions = new InMemoryLocalSessionStore();

        Assert.Equal(SessionStartup.NoSession, await Bootstrapper(sessions).StartAsync());
    }

    [Fact]
    public async Task ItOpensTheAppOnAFreshSessionWithoutTouchingTheNetwork()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-3)));

        Assert.Equal(SessionStartup.Ready, await Bootstrapper(sessions).StartAsync());
        Assert.NotNull(sessions.Current);
    }

    [Fact]
    public async Task ItKeepsASessionSittingExactlyOnTheWindow()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-7)));

        Assert.Equal(SessionStartup.Ready, await Bootstrapper(sessions).StartAsync());
    }

    [Fact]
    public async Task ItExpiresASessionPastTheWindow()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-7).AddSeconds(-1)));
        var replica = new InMemoryReplica();

        Assert.Equal(SessionStartup.NoSession, await Bootstrapper(sessions, replica).StartAsync());
        Assert.Null(sessions.Current);
    }

    [Fact]
    public async Task ItReportsNoSessionWhenTheStoreThrows()
    {
        var sessions = new ThrowingLocalSessionStore();
        var bootstrapper = new SessionBootstrapper(sessions, new InMemoryReplica(), new FixedClock(Now));

        Assert.Equal(SessionStartup.NoSession, await bootstrapper.StartAsync());
    }

    [Fact]
    public async Task ItGivesUpWhenTheSessionStoreNeverAnswers()
    {
        var bootstrapper = new SessionBootstrapper(
            new StallingLocalSessionStore(), new InMemoryReplica(), new FixedClock(Now));

        var start = bootstrapper.StartAsync(TimeSpan.FromMilliseconds(50));
        var finished = await Task.WhenAny(
            start, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

        Assert.Same(start, finished);
        Assert.Equal(SessionStartup.NoSession, await start);
    }

    static SessionBootstrapper Bootstrapper(
        InMemoryLocalSessionStore sessions, InMemoryReplica? replica = null) =>
        new(sessions, replica ?? new InMemoryReplica(), new FixedClock(Now));

    static LocalSession Session(DateTimeOffset lastContact) =>
        new(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "refresh", lastContact);

    sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    sealed class StallingLocalSessionStore : ILocalSessionStore
    {
        public Task<LocalSession?> LoadAsync() => new TaskCompletionSource<LocalSession?>().Task;

        public Task SaveAsync(LocalSession session) => Task.CompletedTask;

        public Task ClearAsync() => Task.CompletedTask;
    }
}
