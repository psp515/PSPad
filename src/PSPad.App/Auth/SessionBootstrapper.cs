using PSPad.Abstractions;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class SessionBootstrapper(ILocalSessionStore sessions, IReplica replica, IClock clock)
{
    public static readonly TimeSpan TrustWindow = TimeSpan.FromDays(7);

    public async Task<SessionStartup> StartAsync()
    {
        var session = await sessions.LoadAsync();

        if (session is null)
        {
            return SessionStartup.NoSession;
        }

        if (clock.UtcNow - session.LastServerContactUtc > TrustWindow)
        {
            // The outbox survives: the replica is re-fetchable from the server, locally authored commands are not.
            await replica.ClearAsync();
            await sessions.ClearAsync();
            return SessionStartup.NoSession;
        }

        return SessionStartup.Ready;
    }
}
