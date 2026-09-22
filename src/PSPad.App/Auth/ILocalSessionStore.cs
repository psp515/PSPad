namespace PSPad.App.Auth;

public interface ILocalSessionStore
{
    Task<LocalSession?> LoadAsync();

    Task SaveAsync(LocalSession session);

    Task ClearAsync();
}
