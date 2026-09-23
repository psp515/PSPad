using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class LocalSignOut(
    ILocalSessionStore sessions,
    IReplica replica,
    LocalAuthenticationStateProvider authenticationState)
{
    public async Task ClearAsync()
    {
        await ClearStorageAsync();
        Announce();
    }

    public void Announce() => authenticationState.SignedOut();

    public async Task ClearStorageAsync()
    {
        try
        {
            // The outbox survives: the replica comes back from the server, locally authored commands do not.
            await replica.ClearAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Replica clear on sign-out failed: {exception.Message}");
        }

        try
        {
            await sessions.ClearAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Session clear on sign-out failed: {exception.Message}");
        }
    }
}
