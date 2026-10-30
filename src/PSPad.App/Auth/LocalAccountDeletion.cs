namespace PSPad.App.Auth;

public sealed class LocalAccountDeletion(LocalDeviceData data, LocalAuthenticationStateProvider authenticationState)
{
    public async Task ClearAsync()
    {
        await ClearStorageAsync();
        Announce();
    }

    public void Announce() => authenticationState.SignedOut();

    public Task ClearStorageAsync() => data.ClearAsync("account deletion");
}
