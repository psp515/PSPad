namespace PSPad.App.Auth;

public static class KeycloakEndSession
{
    public static string UrlFor(string authority, string clientId, string postLogoutRedirectUri) =>
        $"{authority.TrimEnd('/')}/protocol/openid-connect/logout" +
        $"?client_id={Uri.EscapeDataString(clientId)}" +
        $"&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";
}
