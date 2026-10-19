namespace PSPad.App.Auth;

public static class LocalSessionStoreExtensions
{
    public static async Task RememberLanguageAsync(this ILocalSessionStore sessions, string language)
    {
        var session = await sessions.LoadAsync();
        if (session is not null)
        {
            await sessions.SaveAsync(session with { Language = language });
        }
    }
}
