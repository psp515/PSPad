using PSPad.Abstractions;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class SessionBootstrapper(ILocalSessionStore sessions, IReplica replica, IClock clock)
{
    public static readonly TimeSpan TrustWindow = TimeSpan.FromDays(7);

    public async Task<SessionStartup> StartAsync()
    {
        LocalSession? session;
        try
        {
            session = await sessions.LoadAsync();
        }
        catch (Exception)
        {
            return SessionStartup.NoSession;
        }

        if (session is null)
        {
            return SessionStartup.NoSession;
        }

        if (clock.UtcNow - session.LastServerContactUtc > TrustWindow)
        {
            try
            {
                // The outbox survives: the replica is re-fetchable from the server, locally authored commands are not.
                await replica.ClearAsync();
                await sessions.ClearAsync();
            }
            catch (Exception)
            {
            }

            return SessionStartup.NoSession;
        }

        return SessionStartup.Ready;
    }
}
